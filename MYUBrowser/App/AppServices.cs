using Microsoft.Web.WebView2.Core;
using MYUBrowser.Core;

namespace MYUBrowser.App;

/// <summary>
/// 传给内容视图工厂的共享上下文：设置、书签、共享 WebView 环境，以及回到 AppHost 的协调回调。
/// 视图通过它获取依赖，而不自行拼装，避免耦合。
/// </summary>
public sealed class AppServices
{
    public AppServices(AppSettings settings, BookmarkStore bookmarks)
    {
        Settings = settings;
        Bookmarks = bookmarks;
    }

    public AppSettings Settings { get; }
    public BookmarkStore Bookmarks { get; }

    /// <summary>请求切换到某个内容界面（由 AppHost 赋值）。</summary>
    public Action<string> RequestSwitchTo { get; set; } = _ => { };

    /// <summary>弹托盘气泡（timeoutMs, title, text, icon）。由 AppHost 赋值。</summary>
    public Action<int, string, string, ToolTipIcon> ShowBalloon { get; set; } = (_, _, _, _) => { };

    /// <summary>执行一个全局快捷键命令（如透明度/极简）。由 AppHost 赋值。</summary>
    public Func<string, bool> ExecuteShortcut { get; set; } = _ => false;

    /// <summary>统一快捷键注册表（由 AppHost 赋值）。</summary>
    public ShortcutManager? Shortcuts { get; set; }

    /// <summary>以 Shell 为父安全弹出模态对话框（处理 TopMost）。由 AppHost 赋值。</summary>
    public Func<Form, DialogResult> ShowModalDialog { get; set; } = d => d.ShowDialog();

    /// <summary>以 Shell 为父安全弹出通用对话框（如文件选择，处理 TopMost）。由 AppHost 赋值。</summary>
    public Func<CommonDialog, DialogResult> ShowFileDialog { get; set; } = d => d.ShowDialog();

    /// <summary>清理缓存的实现（由持有 WebView 的视图注册）。</summary>
    public Func<Task>? ClearCacheHandler { get; set; }

    /// <summary>设置对话框确认后触发，供视图/窗口重新套用设置。</summary>
    public event Action? SettingsApplied;

    /// <summary>由 AppHost 在设置确认后调用。</summary>
    public void NotifySettingsApplied() => SettingsApplied?.Invoke();

    private CoreWebView2Environment? _env;

    /// <summary>获取共享 WebView2 环境（同一用户数据目录、统一缓存上限），懒创建一次。</summary>
    public async Task<CoreWebView2Environment> GetWebViewEnvironmentAsync()
    {
        if (_env != null) return _env;
        long cacheBytes = Math.Max(10, Settings.DiskCacheLimitMB) * 1024L * 1024L;
        var options = new CoreWebView2EnvironmentOptions
        {
            AdditionalBrowserArguments = $"--disk-cache-size={cacheBytes} --media-cache-size={cacheBytes}",
        };
        _env = await CoreWebView2Environment.CreateAsync(null, AppSettings.WebViewDataDir, options);
        return _env;
    }

    /// <summary>数据目录磁盘占用（无需 WebView，直接枚举）。</summary>
    public static long GetCacheSizeBytes()
    {
        try
        {
            if (!Directory.Exists(AppSettings.WebViewDataDir)) return 0;
            return new DirectoryInfo(AppSettings.WebViewDataDir)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(f => f.Length);
        }
        catch { return 0; }
    }
}
