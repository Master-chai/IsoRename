using System.Text;
using IsoRename;
const string Expected="DEMO01-00000001-LINE0001-E32-EISO-A00-001";
string root=Path.Combine(Path.GetTempPath(),"iso-checks-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
int passed=0;
void Must(bool ok,string name){if(!ok)throw new Exception("FAILED: "+name);Console.WriteLine("PASS: "+name);passed++;}
string Dir(string n)=>Directory.CreateDirectory(Path.Combine(root,n)).FullName;
Scanned Scan(string p)=>new(p,Reader.Inspect(p));
Entry[] Plan(params string[] paths)=>Pairing.Plan(paths.Select(Scan).ToArray());
bool Same(string a,string b)=>Reader.Hash(a)==Reader.Hash(b);
string dxf=args.Length>0?args[0]:Path.Combine(Dir("synthetic"),"test.dxf");
string pdf=args.Length>1?args[1]:Path.Combine(Dir("synthetic"),"different.pdf");
void SyntheticDxf(string path,string number){
 File.WriteAllText(path,$"0\nSECTION\n2\nENTITIES\n0\nTEXT\n10\n533\n20\n23\n40\n2.75\n1\nDEMO-E4-100-PP00-001\n0\nTEXT\n10\n582\n20\n19\n40\n2\n1\n{number[^1]}\n0\nTEXT\n10\n524\n20\n19\n40\n2\n1\n{number[..^1]}\n0\nENDSEC\n0\nEOF\n");
}
void Pdf(string path,string? number=null,bool multi=false){
 var content=number==null?"25 25 m 100 100 l S":$"BT /F1 4 Tf 1510 65 Td (DEMO-E4-100-PP00-001) Tj ET BT /F1 4 Tf 1485.35 53.86 Td ({number}) Tj ET";
 var page="<< /Type /Page /Parent 2 0 R /MediaBox [0 0 1684 1191] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>";
 var objects=new List<string>{"<< /Type /Catalog /Pages 2 0 R >>",multi?"<< /Type /Pages /Kids [3 0 R 6 0 R] /Count 2 >>":"<< /Type /Pages /Kids [3 0 R] /Count 1 >>",page,$"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream","<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"};
 if(multi)objects.Add(page);
 var sb=new StringBuilder("%PDF-1.4\n");var offsets=new List<int>();
 for(int i=0;i<objects.Count;i++){offsets.Add(sb.Length);sb.Append($"{i+1} 0 obj\n{objects[i]}\nendobj\n");}
 int xref=sb.Length;sb.Append($"xref\n0 {offsets.Count+1}\n0000000000 65535 f \n");
 foreach(int o in offsets)sb.Append($"{o:0000000000} 00000 n \n");
 sb.Append($"trailer\n<< /Size {offsets.Count+1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
 File.WriteAllText(path,sb.ToString(),Encoding.ASCII);
}
try{
 if(args.Length==0){SyntheticDxf(dxf,Expected);Pdf(pdf);}
 var dx=Scan(dxf).Result;
 var pp=Scan(pdf).Result;
 Console.WriteLine(dx); Console.WriteLine(pp);
 Must(dx.Number==Expected,"DXF reads full number with detached final digit");
 Must(pp.SinglePagePdf && pp.Number=="" && pp.Fingerprint.Length>0,"vector PDF detected, no OCR");
 if(Path.GetFileNameWithoutExtension(dxf)!=Path.GetFileNameWithoutExtension(pdf))
  Must(Plan(dxf,pdf)[1].Number=="","different original stems never pair");
 var input=Dir("图纸 输入");var dup=Dir("重复来源");var outp=Dir("输出 副本");
 var d=Path.Combine(input,"同名-001.dxf");var p=Path.Combine(input,"同名-001.pdf");
 File.Copy(dxf,d);File.Copy(pdf,p);
 var plan=Plan(p,d);
 Must(plan.All(x=>x.Number==Expected)&&plan[0].Source=="同名 DXF","name-paired vector PDF and DXF share full number irrespective of order");
 var written=Exporter.Write(plan,outp);
 var pd=Path.Combine(outp,Expected+".pdf");var dd=Path.Combine(outp,Expected+".dxf");
 Must(written.Copied==2 && Same(p,pd)&&Same(d,dd)&&Same(p,pdf)&&Same(d,dxf),"two output copies byte-identical, sources unchanged");
 Must(File.ReadAllText(written.Report).Contains("同名 DXF"),"CSV records provenance");
 Must(Exporter.Write(plan,outp).Copied==0&&Same(p,pd)&&Same(d,dd),"rerun never overwrites");
 var half=Dir("half");File.Copy(p,Path.Combine(half,Expected+".pdf"));
 Must(Exporter.Write(plan,half).Copied==0&&!File.Exists(Path.Combine(half,Expected+".dxf")),"existing output blocks paired file too");
 var d2=Path.Combine(dup,"同名-001.dxf");File.Copy(d,d2);
 Must(Plan(p,d,d2).All(x=>x.Number==""),"ambiguous same basenames skipped");
 var p2=Path.Combine(input,"同名-002.pdf");var d22=Path.Combine(input,"同名-002.dxf");
 Pdf(p2);SyntheticDxf(d22,Expected[..^1]+"2");
 var group=Plan(p2,d,p,d22);
 Must(group[0].Number.EndsWith("002") && group[2].Number.EndsWith("001"),"multiple sheets use their own printed page digits");
 var d3=Path.Combine(input,"another.dxf");SyntheticDxf(d3,Expected);
 Must(Exporter.Write(Plan(p,d,d3),Dir("dup-output")).Copied==0,"two same target DXFs also block paired PDF");
 var standalone=Path.Combine(input,"standalone.pdf");Pdf(standalone,Expected);
 Must(Plan(standalone)[0].Number==Expected,"text PDF reads text without OCR");
 var pdMismatch=Path.Combine(input,"another.pdf");Pdf(pdMismatch,Expected[..^1]+"9");
 Must(Plan(d3,pdMismatch).All(x=>x.Number==""),"readable PDF disagreeing with DXF skips both");
 Pdf(pdMismatch,multi:true);
 Must(Plan(d3,pdMismatch).All(x=>x.Number==""),"multi-page PDF skipped");
 var old=Path.Combine(input,"old-only.dxf");File.WriteAllText(old,"0\nSECTION\n2\nENTITIES\n0\nTEXT\n10\n533\n20\n23\n40\n2.75\n1\nDEMO-E4-100-PP00-001\n0\nENDSEC\n0\nEOF\n");
 Must(Plan(old)[0].Number=="","old upper identifier never used");
 var stale=Plan(p,d);File.AppendAllText(d," ");
 Must(Exporter.Write(stale,Dir("stale")).Copied==0,"source changed after preview blocks pair");
 var afterPdf=Path.Combine(input,"newpair.pdf");var afterDxf=Path.Combine(input,"newpair.dxf");
 File.Copy(pdf,afterPdf);File.Copy(dxf,afterDxf);
 var changedPdf=Plan(afterDxf,afterPdf);File.AppendAllText(afterPdf," ");
 Must(Exporter.Write(changedPdf,Dir("changed-pdf")).Copied==0,"changed PDF cannot cause partial output of paired DXF");
 Console.WriteLine($"PASS {passed} checks");
} finally {Directory.Delete(root,true);}
