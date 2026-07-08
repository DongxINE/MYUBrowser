using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MYUBrowser.Core;

/// <summary>
/// 本地文档加载：把 txt / md / epub 解析为纯文本。仅负责“取文本”，不含任何 UI。
/// 新增格式只需在 Load 里加一个分支。
/// </summary>
public static class DocumentLoader
{
    public static readonly string[] SupportedExtensions = { ".txt", ".md", ".markdown", ".epub" };

    public static string OpenFileDialogFilter =>
        "支持的文档|*.txt;*.md;*.markdown;*.epub|文本 (*.txt)|*.txt|Markdown (*.md;*.markdown)|*.md;*.markdown|EPUB (*.epub)|*.epub|所有文件|*.*";

    public static bool IsSupported(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    /// <summary>读取并返回纯文本；失败抛异常由调用方处理。</summary>
    public static string Load(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".epub" => LoadEpub(path),
            ".md" or ".markdown" => StripMarkdown(ReadAllTextSmart(path)),
            _ => ReadAllTextSmart(path),
        };
    }

    /// <summary>尽量正确地读取文本编码（优先 BOM，否则 UTF-8）。</summary>
    private static string ReadAllTextSmart(string path)
    {
        var bytes = File.ReadAllBytes(path);
        // 有 BOM 时 StreamReader 会自动识别
        using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static string StripMarkdown(string md)
    {
        // 轻量清理：去掉常见标记符号，保留可读文本结构
        string s = md;
        s = Regex.Replace(s, @"^#{1,6}\s*", "", RegexOptions.Multiline);   // 标题 #
        s = Regex.Replace(s, @"(\*\*|__)(.+?)\1", "$2");                    // 粗体
        s = Regex.Replace(s, @"(\*|_)(.+?)\1", "$2");                       // 斜体
        s = Regex.Replace(s, @"`{1,3}([^`]*)`{1,3}", "$1");                 // 行内/围栏代码
        s = Regex.Replace(s, @"!\[[^\]]*\]\([^)]*\)", "");                  // 图片
        s = Regex.Replace(s, @"\[([^\]]+)\]\([^)]*\)", "$1");               // 链接保留文字
        s = Regex.Replace(s, @"^\s{0,3}>\s?", "", RegexOptions.Multiline);  // 引用
        s = Regex.Replace(s, @"^\s*[-*+]\s+", "• ", RegexOptions.Multiline);// 列表
        return s;
    }

    // ================= EPUB =================

    private static string LoadEpub(string path)
    {
        using var zip = ZipFile.OpenRead(path);

        string opfPath = FindOpfPath(zip);
        var opfEntry = GetEntry(zip, opfPath) ?? throw new FileFormatException("EPUB 缺少 OPF 清单");
        string opfDir = GetDirectory(opfPath);

        XDocument opf;
        using (var s = opfEntry.Open()) opf = XDocument.Load(s);

        XNamespace opfNs = opf.Root!.Name.Namespace;

        // manifest: id -> href
        var manifest = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var manifestEl = opf.Root.Element(opfNs + "manifest");
        if (manifestEl != null)
        {
            foreach (var item in manifestEl.Elements(opfNs + "item"))
            {
                string? id = item.Attribute("id")?.Value;
                string? href = item.Attribute("href")?.Value;
                if (id != null && href != null) manifest[id] = href;
            }
        }

        // spine: 阅读顺序
        var sb = new StringBuilder();
        var spineEl = opf.Root.Element(opfNs + "spine");
        if (spineEl != null)
        {
            foreach (var itemref in spineEl.Elements(opfNs + "itemref"))
            {
                string? idref = itemref.Attribute("idref")?.Value;
                if (idref == null || !manifest.TryGetValue(idref, out var href)) continue;

                string entryPath = CombineZipPath(opfDir, Uri.UnescapeDataString(href));
                var entry = GetEntry(zip, entryPath);
                if (entry == null) continue;

                string html;
                using (var es = entry.Open())
                using (var r = new StreamReader(es, Encoding.UTF8, true))
                    html = r.ReadToEnd();

                string text = HtmlToText(html);
                if (text.Length > 0)
                {
                    sb.Append(text);
                    sb.Append("\n\n");
                }
            }
        }

        string result = sb.ToString().Trim();
        return result.Length > 0 ? result : "（EPUB 已打开，但未解析到可显示的文本内容）";
    }

    private static string FindOpfPath(ZipArchive zip)
    {
        var container = GetEntry(zip, "META-INF/container.xml");
        if (container != null)
        {
            try
            {
                XDocument doc;
                using (var s = container.Open()) doc = XDocument.Load(s);
                XNamespace ns = "urn:oasis:names:tc:opendocument:xmlns:container";
                var rootfile = doc.Descendants(ns + "rootfile").FirstOrDefault();
                string? fullPath = rootfile?.Attribute("full-path")?.Value;
                if (!string.IsNullOrEmpty(fullPath)) return fullPath!;
            }
            catch { }
        }
        // 兜底：直接找任意 .opf
        var any = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith(".opf", StringComparison.OrdinalIgnoreCase));
        return any?.FullName ?? throw new FileFormatException("不是有效的 EPUB 文件");
    }

    private static ZipArchiveEntry? GetEntry(ZipArchive zip, string path)
    {
        path = path.Replace('\\', '/').TrimStart('/');
        return zip.Entries.FirstOrDefault(e =>
            string.Equals(e.FullName.Replace('\\', '/'), path, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetDirectory(string zipPath)
    {
        int i = zipPath.Replace('\\', '/').LastIndexOf('/');
        return i < 0 ? "" : zipPath[..i];
    }

    private static string CombineZipPath(string dir, string rel)
    {
        if (string.IsNullOrEmpty(dir)) return rel;
        var parts = new List<string>(dir.Split('/'));
        foreach (var seg in rel.Split('/'))
        {
            if (seg == "." || seg.Length == 0) continue;
            if (seg == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); }
            else parts.Add(seg);
        }
        return string.Join('/', parts);
    }

    private static string HtmlToText(string html)
    {
        string s = html;
        s = Regex.Replace(s, @"<(script|style)[^>]*>.*?</\1>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        // 段落/换行标签转为换行
        s = Regex.Replace(s, @"</\s*(p|div|h[1-6]|li|br|tr)\s*>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*br\s*/?>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<[^>]+>", "");           // 去掉其余标签
        s = System.Net.WebUtility.HtmlDecode(s);        // 实体解码
        s = Regex.Replace(s, @"[ \t]+", " ");           // 压缩空白
        s = Regex.Replace(s, @"\n{3,}", "\n\n");        // 压缩多余空行
        return s.Trim();
    }
}
