using System.IO;
namespace IsoRename;
public sealed record Scanned(string Path,Inspection Result);
public static class Pairing {
 public static Entry[] Plan(IReadOnlyList<Scanned> scans){
  var entries=scans.Select(s=>new Entry(s.Path,s.Result.Number,s.Result.Source,s.Result.Fingerprint,s.Result.Status)).ToArray();
  bool IsDxf(int i)=>Path.GetExtension(scans[i].Path).Equals(".dxf",StringComparison.OrdinalIgnoreCase);
  foreach(var group in Enumerable.Range(0,scans.Count).GroupBy(i=>Path.GetFileNameWithoutExtension(scans[i].Path),StringComparer.OrdinalIgnoreCase)){
   var list=group.ToArray();
   if(list.Length==1)continue;
   var d=list.Where(IsDxf).ToArray();var p=list.Where(i=>!IsDxf(i)).ToArray();
   void Block(string msg){foreach(int i in list)entries[i]=entries[i] with{Number="",Status=msg};}
   if(d.Length!=1||p.Length!=1){Block("跳过：同名文件不唯一");continue;}
   int di=d[0],pi=p[0];var dx=scans[di].Result;var pdf=scans[pi].Result;
   if(!Reader.Valid(dx.Number)||dx.Fingerprint.Length==0){Block("跳过：同名 DXF 未读到有效档案号");continue;}
   if(!pdf.SinglePagePdf||pdf.Fingerprint.Length==0){Block("跳过：同名 PDF 无法读取或不是单页");continue;}
   if(pdf.Number.Length>0 && pdf.Number!=dx.Number){Block("跳过：PDF 与同名 DXF 档案号不一致");continue;}
   entries[di]=entries[di] with{Group=di};
   entries[pi]=entries[pi] with{Number=dx.Number,Source="同名 DXF",Status="已配对",Group=di,PeerFingerprint=dx.Fingerprint,PeerPath=scans[di].Path};
  }
  return entries;
 }
}
