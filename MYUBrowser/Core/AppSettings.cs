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

    // ---- 老板键（默认 Alt+Q，作为快捷键系统 boss.toggle 的兼容字段）----
    public uint BossKeyModifiers { get; set; } = HotkeyManager.MOD_ALT;
    public uint BossKeyVirtualKey { get; set; } = (uint)Keys.Q;

    // ---- 快捷键绑定：命令 Id -> (int)Keys；缺省项回退到命令默认键 ----
    public Dictionary<string, int> ShortcutBindings { get; set; } = new();

    // ---- 行为 ----
    public bool CloseToTray { get; set; }
    public bool Muted { get; set; }

    // ---- 番茄钟 ----
    public bool PomodoroNotify { get; set; } = true;         // 阶段切换托盘提醒
    public int PomodoroMinutes { get; set; } = 25;           // 专注时长（分钟）
    public int PomodoroBreakMinutes { get; set; } = 5;       // 短休息（分钟）
    public int PomodoroLongBreakMinutes { get; set; } = 15;  // 长休息（分钟）
    public int PomodorosPerLongBreak { get; set; } = 4;      // 每几个专注后长休息
    public bool PomodoroAutoStartNext { get; set; }          // 阶段结束自动开始下一段
    public bool PomodoroDimOnFocus { get; set; }             // 专注期自动降低透明度
    public int PomodoroFocusDimOpacity { get; set; } = 40;   // 专注期目标透明度

    // ---- 待办截止提醒 ----
    public bool TodoRemindEnabled { get; set; } = true;      // 临近截止弹托盘提醒
    public int TodoReminderLeadMinutes { get; set; } = 60;   // 提前多少分钟提醒

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
