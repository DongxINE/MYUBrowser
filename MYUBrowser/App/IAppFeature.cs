namespace MYUBrowser.App;

/// <summary>
/// 横切功能模块（番茄钟/音频卫士/统计…）。在 Attach 中订阅 AppHost 的事件中枢，
/// 与任何窗口/视图解耦。新增功能只需实现该接口并 AddFeature 注册。
/// </summary>
public interface IAppFeature : IDisposable
{
    void Attach(AppHost host);
}
