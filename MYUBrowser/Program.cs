using MYUBrowser.App;
using MYUBrowser.App.Features;
using MYUBrowser.Core;
using MYUBrowser.UI;
using MYUBrowser.Views;
using Velopack;

namespace MYUBrowser;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        VelopackApp.Build().Run();

        // 单实例运行，避免多开导致老板键注册冲突
        using var mutex = new Mutex(true, "MYUBrowser_SingleInstance_Mutex", out bool createdNew);
        if (!createdNew) return;

        ApplicationConfiguration.Initialize();

        var host = new AppHost();

        // 共享待办存储：番茄钟与截止提醒共用同一份数据
        var todos = TodoStore.Load();

        var pomodoro = new PomodoroFeature(host.Settings, todos);
        host.AddFeature(pomodoro);
        host.AddFeature(new TaskReminderFeature(host.Settings, todos));

        // 拓展轴 A：注册内容界面（新增界面只需加一行）
        host.RegisterView(new ContentKind("browser", "浏览器", "🌐", s => new BrowserView(s)));
        host.RegisterView(new ContentKind("reader", "阅读器", "📖", s => new ReaderView(s)));
        host.RegisterView(new ContentKind("pomodoro", "番茄钟", "🍅", s => new PomodoroView(s, pomodoro)));

        host.Run(defaultViewId: "browser");
    }
}
