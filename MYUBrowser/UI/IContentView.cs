namespace MYUBrowser.UI;

/// <summary>
/// 一个可切换的内容界面（浏览器/阅读器/视频…）。由 ShellForm 托管，
/// 任意时刻只有一个视图处于激活可见状态，其余挂起保留。
/// </summary>
public interface IContentView
{
    /// <summary>承载该视图的实际控件（通常是一个 UserControl，Dock=Fill）。</summary>
    Control Control { get; }

    /// <summary>供标题栏/伪装使用的标题。</summary>
    string Title { get; }

    /// <summary>标题变化时触发（如浏览器文档标题变更）。</summary>
    event Action<string?>? TitleChanged;

    /// <summary>首次显示前的异步初始化（如 WebView），全生命周期只调用一次。</summary>
    Task InitializeAsync();

    /// <summary>被切入、成为当前可见视图时调用（恢复/夺焦）。</summary>
    void OnActivated();

    /// <summary>被切走时调用（挂起保留状态，不销毁）。</summary>
    void OnSuspended();

    /// <summary>老板键隐藏前调用（暂停媒体等）。</summary>
    Task OnHidingAsync();

    /// <summary>老板键唤回后调用（恢复媒体等）。</summary>
    void OnWoke();
}
