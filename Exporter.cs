using System.Globalization;
using System.IO;
using System.Text;
namespace IsoRename;

public sealed record Entry(string Path,string Number,string Source,string Fingerprint,string Status,int Group=-1,string? PeerPath=null,string? PeerFingerprint=null);
public sealed record ExportResult(int Copied,string Report);
public static class Exporter {
 public static string Name(Entry r)=>Reader.Valid(r.Number)&&Reader.Allowed(r.Path)?r.Number+Path.GetExtension(r.Path):"";
 public static string[] Status(IReadOnlyList<Entry> checks,string? output){
  var groups=checks.Select(Name).Where(s=>s.Length>0).GroupBy(s=>s,StringComparer.OrdinalIgnoreCase);
  var duplicates=groups.Where(g=>g.Skip(1).Any()).Select(g=>g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
  var result=checks.Select(r=>{
   var n=Name(r);
   if(n.Length==0||r.Fingerprint.Length==0)return r.Status;
   if(duplicates.Contains(n))return "跳过：批次内重名";
   if(!string.IsNullOrEmpty(output)){var dest=Path.Combine(output,n);if(File.Exists(dest)||Directory.Exists(dest))return "跳过：输出文件已存在";}
   return r.Status;
  }).ToArray();
  foreach(var group in Enumerable.Range(0,checks.Count).Where(i=>checks[i].Group>=0).GroupBy(i=>checks[i].Group)){
   if(group.Any(i=>result[i]!=checks[i].Status))
    foreach(var i in group) if(result[i]==checks[i].Status)result[i]="跳过：同对图纸存在输出冲突";
  }
  return result;
 }
 public static ExportResult Write(IReadOnlyList<Entry> rows,string output){
  if(!Path.IsPathFullyQualified(output)||!Directory.Exists(output))throw new IOException("输出位置须为已存在的绝对路径。");
  var status=Status(rows,output);
  string report=Path.Combine(output,"ISO-归档记录-"+DateTime.Now.ToString("yyyyMMdd-HHmmss",CultureInfo.InvariantCulture)+"-"+Guid.NewGuid().ToString("N")[..8]+".csv");
  using var stream=new FileStream(report,FileMode.CreateNew,FileAccess.Write,FileShare.Read);
  using var log=new StreamWriter(stream,new UTF8Encoding(true)){AutoFlush=true};
  log.WriteLine("原文件,档案号,目标文件名,来源,结果");
  int copied=0;
  var groups=Enumerable.Range(0,rows.Count).GroupBy(i=>rows[i].Group>=0 ? rows[i].Group : -1-i);
  foreach(var group in groups){
   var indexes=group.ToArray();
   if(indexes.All(i=>Name(rows[i]).Length>0 && rows[i].Fingerprint.Length>0 && status[i]==rows[i].Status)){
    var stage=new List<(string Temp,string Target,string Fingerprint)>();
    var completed=new List<(string Target,string Fingerprint)>();
    try{
     foreach(var i in indexes){
      var r=rows[i];
      if(r.PeerPath!=null&&Reader.Hash(r.PeerPath)!=r.PeerFingerprint)throw new IOException("同名 DXF 在预览后发生变化，请重新识别。");
      if(Reader.Hash(r.Path)!=r.Fingerprint)throw new IOException("原图在预览后发生变化，请重新识别。");
      var temp=Path.Combine(output,$".iso-{Guid.NewGuid():N}.tmp");
      stage.Add((temp,Path.Combine(output,Name(r)),r.Fingerprint));
      File.Copy(r.Path,temp,false);
      if(Reader.Hash(temp)!=r.Fingerprint)throw new IOException("原图在复制时发生变化，请重新识别。");
     }
     foreach(var item in stage){
      File.Move(item.Temp,item.Target,false);completed.Add((item.Target,item.Fingerprint));
     }
     copied+=indexes.Length;
     foreach(int i in indexes)status[i]="已输出";
    }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException){
     int remainder=0;
     foreach(var item in completed){
      try{
       if(!File.Exists(item.Target))continue;
       if(Reader.Hash(item.Target)==item.Fingerprint)File.Delete(item.Target);else remainder++;
      }catch(Exception cleanup) when(cleanup is IOException or UnauthorizedAccessException){remainder++;}
     }
     copied+=remainder;
     foreach(int i in indexes)
      status[i]=remainder==0?"跳过："+ex.Message:"部分输出未能清理，请检查文件夹："+ex.Message;
    }finally{
     foreach(var item in stage)try{File.Delete(item.Temp);}catch(IOException){}catch(UnauthorizedAccessException){}
    }
   }
   foreach(var i in indexes){
    var r=rows[i];
    log.WriteLine(string.Join(",",new[]{r.Path,r.Number,Name(r),r.Source,status[i]}.Select(Quote)));
   }
  }
  return new(copied,report);
 }
 static string Quote(string s){
  if(s.TrimStart().FirstOrDefault() is '=' or '-' or '+' or '@')s="'"+s;
  return "\""+s.Replace("\"","\"\"")+"\"";
 }
}
