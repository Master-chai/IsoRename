using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
namespace IsoRename;

public sealed record Inspection(string Number,string Status,string Source,string Fingerprint,bool SinglePagePdf);
public static partial class Reader {
 [GeneratedRegex(@"\A[A-Z0-9]+(?:-[A-Z0-9]+)*-EISO-[A-Z0-9]+-[0-9]{3}\z",RegexOptions.CultureInvariant)]
 private static partial Regex Pattern();
 [GeneratedRegex(@"-EISO-[A-Z0-9]+-[0-9]{2}\z",RegexOptions.CultureInvariant)]
 private static partial Regex DxfPrefix();
 public static bool Valid(string s)=>s.Length is >=12 and <=180 && Pattern().IsMatch(s);
 public static bool Allowed(string p)=>Path.GetExtension(p).ToLowerInvariant() is ".dxf" or ".pdf";
 public static string Hash(string p){using var f=File.OpenRead(p);return Convert.ToHexString(SHA256.HashData(f));}
 public static Inspection Inspect(string p) {
  string ext=Path.GetExtension(p).ToLowerInvariant(), source=ext==".dxf"?"DXF 文字":"PDF 文字";
  try {
   var before=Hash(p);
   string n, status; bool can;
   if(ext==".dxf"){n=Dxf(p);status="已识别";can=false;}
   else if(ext==".pdf"){(n,status,can)=Pdf(p);}
   else throw new InvalidDataException("只接受 DXF 和 PDF。");
   if(Hash(p)!=before) throw new IOException("扫描期间原图发生变化，请重新识别。");
   return new(n,status,n.Length>0?source:"",before,can);
  } catch(Exception ex) when(ex is not OutOfMemoryException) {
   return new("","未识别："+ex.Message,"","",false);
  }
 }
 static double Num(Dictionary<int,string> d,int k)=>d.TryGetValue(k,out var s)&&double.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out var n)?n:double.NaN;
 static bool Near(double x,double target,double tol)=>Math.Abs(x-target)<=tol+1e-8;
 static string Dxf(string path){
  // Preserve v0.3's strict two-TEXT profile, verified against the actual PDMS export.
  var hits=new List<(double X,string Text)>(); var d=new Dictionary<int,string>(); string type=""; bool entities=false, seen=false,eof=false;
  void Flush(){if(entities&&type=="TEXT"&&Near(Num(d,20),19,.10)&&Near(Num(d,40),2,.05)&&Num(d,10)>=523.9&&Num(d,10)<=582.1&&d.TryGetValue(1,out var t))hits.Add((Num(d,10),t.Trim()));}
  using var r=new StreamReader(path); string? k;
  while((k=r.ReadLine())!=null){
   var v=r.ReadLine()??throw new InvalidDataException("DXF 组码不完整。");
   if(!int.TryParse(k.Trim(),NumberStyles.Integer,CultureInfo.InvariantCulture,out int code)) throw new InvalidDataException("此版只支持 PDMS 导出的 ASCII DXF。");
   v=v.Trim();
   if(code==0){Flush();d.Clear();type=v;if(v=="ENDSEC")entities=false;if(v=="EOF"){eof=true;break;}}
   else {d[code]=v;if(type=="SECTION"&&code==2){entities=v=="ENTITIES";seen|=entities;}}
  }
  if(!seen||!eof||entities) throw new InvalidDataException("DXF 结构不完整。");
  var ordered=hits.OrderBy(h=>h.X).ToArray();
  if(ordered.Length!=2||!Near(ordered[0].X,524,.1)||!Near(ordered[1].X,582,.1)) throw new InvalidDataException("未找到样板中的两段文字（X=524／582，Y=19，字高 2）；不会猜测编号。");
  if(!DxfPrefix().IsMatch(ordered[0].Text)||ordered[1].Text.Length!=1||!char.IsAsciiDigit(ordered[1].Text[0])) throw new InvalidDataException("档案号主体或末位页码不完整。");
  var n=ordered[0].Text+ordered[1].Text;
  return Valid(n)?n:throw new InvalidDataException("档案号格式不符。");
 }
 static (string,string,bool) Pdf(string path){
  using var doc=PdfDocument.Open(path);
  if(doc.NumberOfPages!=1) return("","多页 PDF，请先拆成一图一文件。",false);
  var p=doc.GetPage(1); const double pt=72.0/25.4;
  var letters=p.Letters.Where(l=>Math.Abs(l.StartBaseLine.Y-19*pt)<=2 && l.StartBaseLine.X>=523*pt && l.StartBaseLine.X<=594*pt
      && Math.Abs(l.EndBaseLine.Y-l.StartBaseLine.Y)<=.5 && !string.IsNullOrWhiteSpace(l.Value))
      .OrderBy(l=>l.StartBaseLine.X).ToArray();
  if(letters.Length==0) return("","标题栏无文字；等待同名 DXF。",true);
  if(letters.Max(l=>l.StartBaseLine.Y)-letters.Min(l=>l.StartBaseLine.Y)>2 || letters[0].StartBaseLine.X>582.1*pt)
      return("","PDF 目标区域未能确认；等待同名 DXF。",true);
  var s=string.Concat(letters.Select(l=>l.Value)).Replace("\u00AD","").Replace(" ","").Trim();
  return Valid(s)?(s,"已识别",true):("","PDF 未读到完整编号；等待同名 DXF。",true);
 }
}
