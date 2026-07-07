using System.Text.Json;
using System.Text.Json.Serialization;

namespace MYUBrowser.Core;

/// <summary>
/// 应用设置。便携模式：所有数据/缓存都放在程序目录下的 Data 子文件夹，
/// 若程序目录不可写（如装在 Program Files）自动回退到 %LocalAppData%\MYUBrowser。
/// </summary>
public sealed class AppSettings
{
    public static string DataDir { get; } = ResolveDataDir();

    public static string WebViewDataDir { get; } = Path.Combine(DataDir, "WebView2");

    private static string SettingsPath => Path.Combine(DataDir, "settings.json");

    private static string ResolveDataDir()
    {
        string portable = Path.Combine(AppContext.BaseDirectory, "Data");
        try
        {
            Directory.CreateDirectory(portable);
            // 写入探针文件验证目录可写
            string probe = Path.Combine(portable, ".write_test");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return portable;
        }
        catch
        {
            string fallback = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MYUBrowser");
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }

    // ---- 浏览 ----
    public string HomeUrl { get; set; } = "https://www.bing.com";
    public string? LastUrl { get; set; }
    public string SearchUrlTemplate { get; set; } = "https://www.bing.com/search?q={0}";
    public double ZoomFactor { get; set; } = 1.0;

    // ---- 外观 ----
    public int OpacityPercent { get; set; } = 100;          // 0-100，无下限限制
    public bool ForceDarkFilter { get; set; }                // CSS 反色滤镜深色
    public bool HideImages { get; set; }                     // 图片/视频淡化
    public bool UseFakeTitle { get; set; } = true;
    public string FakeTitle { get; set; } = "文档1 - Word";
    public bool AutoDimOnMouseLeave { get; set; }
    public int AutoDimOpacityPercent { get; set; } = 20;

    // ---- 老板键（默认 Alt+Q）----
    public uint BossKeyModifiers { get; set; } = HotkeyManager.MOD_ALT;
    public uint BossKeyVirtualKey { get; set; } = (uint)Keys.Q;

    // ---- 行为 ----
    public bool CloseToTray { get; set; }
    public bool Muted { get; set; }

    // ---- 缓存/性能 ----
    public bool ClearCacheOnExit { get; set; } = true;
    public int DiskCacheLimitMB { get; set; } = 100;

    // ---- 更新 ----
    public string UpdateRepoUrl { get; set; } = "https://github.com/DongxINE/MYUBrowser";
    public bool UpdatePrerelease { get; set; }

    // ---- 窗口 ----
    public int WindowX { get; set; } = -1;
    public int WindowY { get; set; } = -1;
    public int WindowWidth { get; set; } = 1100;
    public int WindowHeight { get; set; } = 720;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings();
        }
        catch { /* 配置损坏时回退默认值 */ }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOpts));
        }
        catch { /* 保存失败不影响使用 */ }
    }
}
