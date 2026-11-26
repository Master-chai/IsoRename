using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
namespace IsoRename;
public partial class MainWindow:Window {
 public record Item(string Path,Inspection Scan);
 public record Row(Entry Entry,string Status) {
  public string Original=>Path.GetFileName(Entry.Path);
  public string NewName=>Exporter.Name(Entry);
  public string Source=>Entry.Source;
 }
 Entry[] Entries=>Pairing.Plan(rows.Select(r=>new Scanned(r.Path,r.Scan)).ToArray());
 readonly List<Item> rows=new();
 readonly ObservableCollection<Row> view=new();
 bool ready,busy;
 public MainWindow(){
  InitializeComponent();Table.ItemsSource=view;
  try{if(File.Exists(SettingsFile))OutputPath.Text=File.ReadAllText(SettingsFile).Trim();}catch(IOException){}catch(UnauthorizedAccessException){}
  ready=true;Refresh();
 }
 static string SettingsFile=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"IsoRename","output-path.txt");
 void RememberOutput(string path){
  try{Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);File.WriteAllText(SettingsFile,path);}
  catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){Summary.Text="输出路径未能保存："+ex.Message;}
 }
 void Error(string message)=>MessageBox.Show(this,message,"ISO 图纸归档",MessageBoxButton.OK,MessageBoxImage.Information);
 string? ValidOutput(bool show=false){
  try{var raw=OutputPath.Text.Trim();if(!Path.IsPathFullyQualified(raw)||!Directory.Exists(raw))throw new IOException("请选择有效的、已存在的输出文件夹。");return Path.GetFullPath(raw);}
  catch(Exception ex) when(ex is IOException or ArgumentException or NotSupportedException){if(show)Error(ex.Message);return null;}
 }
 bool CanExport(Row row)=>row.NewName.Length>0 && row.Entry.Fingerprint.Length>0 && row.Status==row.Entry.Status;
 void Refresh(){
  if(!ready||busy)return;
  var prev=(Table.SelectedItem as Row)?.Entry.Path;
  var plan=Entries;
  var status=Exporter.Status(plan,ValidOutput());
  var all=plan.Select((r,i)=>new Row(r,status[i])).ToArray();
  view.Clear();
  foreach(var r in all.Where(r=>Pending.IsChecked!=true||!CanExport(r)))view.Add(r);
  if(prev!=null)Table.SelectedItem=view.FirstOrDefault(r=>r.Entry.Path==prev);
  int good=all.Count(CanExport);
  Summary.Text=$"共 {all.Length} 份，{good} 份可输出，{all.Length-good} 份待处理。";
  ExportButton.IsEnabled=good>0 && ValidOutput()!=null;
  UpdateSelection();
 }
 void PathChanged(object s,System.Windows.Controls.TextChangedEventArgs e)=>Refresh();
 void FilterChanged(object s,RoutedEventArgs e)=>Refresh();
 void SelectionChanged(object s,System.Windows.Controls.SelectionChangedEventArgs e)=>UpdateSelection();
 void UpdateSelection(){if(SourceButton!=null&&Table!=null)SourceButton.IsEnabled=Table.SelectedItem is Row;}
 async Task Load(IEnumerable<string> paths,bool replace=false){
  if(busy)return;
  try{
   var files=paths.Where(Reader.Allowed).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase)
    .Where(p=>replace||!rows.Any(r=>StringComparer.OrdinalIgnoreCase.Equals(r.Path,p))).ToArray();
   if(files.Length==0)return;
   busy=true; IsEnabled=false; Summary.Text=$"正在读取 {files.Length} 份图纸…";
   var results=await Task.Run(()=>files.Select(p=>new Item(p,Reader.Inspect(p))).ToArray());
   if(replace) rows.Clear();
   rows.AddRange(results);
  }catch(Exception ex){Error(ex.Message);}
  finally{busy=false;IsEnabled=true;Refresh();}
 }
 async void Add(object s,RoutedEventArgs e){var d=new OpenFileDialog{Multiselect=true,Filter="ISO 图纸|*.dxf;*.pdf"};if(d.ShowDialog(this)==true)await Load(d.FileNames);}
 async void Folder(object s,RoutedEventArgs e){var d=new OpenFolderDialog();if(d.ShowDialog(this)==true)await Load(Directory.EnumerateFiles(d.FolderName));}
 async void DropFiles(object s,DragEventArgs e){
  if(e.Data.GetData(DataFormats.FileDrop) is not string[] list)return;
  e.Handled=true;await Load(list.SelectMany(p=>Directory.Exists(p)?Directory.EnumerateFiles(p):new[]{p}));
 }
 void Clear(object s,RoutedEventArgs e){rows.Clear();Refresh();}
 async void Scan(object s,RoutedEventArgs e)=>await Load(rows.Select(r=>r.Path),true);
 void BrowseOutput(object s,RoutedEventArgs e){
  var d=new OpenFolderDialog();if(ValidOutput() is string path)d.InitialDirectory=path;
  if(d.ShowDialog(this)==true){OutputPath.Text=d.FolderName;RememberOutput(d.FolderName);}
 }
 void OpenPath(string path){
  try{Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}catch(Exception ex){Error(ex.Message);}
 }
 void OpenOutput(object s,RoutedEventArgs e){if(ValidOutput(true) is string path)OpenPath(path);}
 void OpenSource(object s,RoutedEventArgs e){if(Table.SelectedItem is Row r)OpenPath(r.Entry.Path);}
 async void Export(object s,RoutedEventArgs e){
  if(busy||ValidOutput(true) is not string output)return;
  var entries=Entries;
  var states=Exporter.Status(entries,output);
  int count=entries.Where((r,i)=>Exporter.Name(r).Length>0&&r.Fingerprint.Length>0&&states[i]==r.Status).Count();
  if(count==0){Refresh();Error("没有可输出的图纸。若有重名，请先核对或更换输出文件夹。");return;}
  if(MessageBox.Show(this,$"将最多输出 {count} 份副本到：\n{output}\n\n现有文件和原图不会被覆盖。","确认输出",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
  RememberOutput(output);
  string? result=null;
  try{
   busy=true;IsEnabled=false;Summary.Text="正在复制并写入记录…";
   var done=await Task.Run(()=>Exporter.Write(entries,output));
   result=$"已输出 {done.Copied} 份，跳过 {entries.Length-done.Copied} 份。记录：{Path.GetFileName(done.Report)}";
  } catch(Exception ex){Error("输出未正常完成："+ex.Message);}
  finally{busy=false;IsEnabled=true;Refresh();if(result!=null)Summary.Text=result;}
 }
}
