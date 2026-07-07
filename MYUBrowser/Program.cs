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
        Application.Run(new MainForm());
    }
}
