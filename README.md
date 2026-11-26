# ISO 图纸归档 v1.0
用于批量按图纸档案号归档 ISO 图纸的 Windows 工具，基于 .NET 10 + WPF。

## 使用
1. Windows 10（22H2）或 Windows 11 的 x64 电脑：完整解压 ZIP，双击 `IsoRename.exe`。便携版已内置 .NET，不用另外安装。未做代码签名；如单位电脑限制未签名程序，请按 IT 的流程处理。
2. 同时把 PDF、DXF 或包含它们的文件夹拖进窗口。导入文件夹只扫该层文件，不扫子文件夹。每张 PDF 必须独立为一个文件。
3. 同一张 ISO 图原文件的名称必须只差扩展名，例如 `管线-01.pdf` 和 `管线-01.dxf`。不按管线号、前缀或文件顺序猜测。Windows 文件名大小写差异不影响配对。一个名称出现多个同格式文件时会跳过该组。
4. 选择输出文件夹。确认预览中的新旧文件名后，点击「输出可归档副本」。输出为文件副本，仍保留原图、原格式和内容。浏览选择或确认输出时会记住文件夹。输出目录有 CSV 记录。

## 规则及限制
- 首选按 v0.3 已验证的 PDMS ASCII DXF 样板读取正式档案号：Y=19 mm、字高 2，两段 TEXT 的起点为 X=524、582 mm。首段保留中间字段，末位从图纸读取，不顺排页码；上方旧编号不使用。
- 同名 PDF 沿用 DXF 的完整档案号。可直接读取文字层的 PDF 支持单独处理；若与配对 DXF 可读档案号冲突，则两份都跳过。无文字层且没有同名 DXF、未读到编号、格式不符和重名的图纸均跳过。不做 OCR，不需录入编号，不改写 PDF 或 DXF。
- 如果通过 Windows 资源管理器调整输出目录中的文件，重新预览后再输出。碰到与样板不一致的 DXF，当前版保守跳过。

源码按 `dotnet publish IsoRename.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true` 构建。核心回归：`dotnet run --project checks`。使用 [PdfPig](https://github.com/UglyToad/PdfPig) 读取 PDF 的文字层和页数。

项目源码不对外授权。第三方依赖 PdfPig 仍按其 Apache-2.0 许可证使用。
