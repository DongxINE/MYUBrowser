using MYUBrowser.App;

namespace MYUBrowser.UI;

/// <summary>
/// 内容界面类型描述符：元数据 + 工厂。新增一种界面只需注册一个 ContentKind，
/// 模式切换入口/托盘菜单/老板键调度会自动生效（开闭原则）。
/// </summary>
public sealed record ContentKind(
    string Id,
    string Title,
    string Glyph,
    Func<AppServices, IContentView> Factory);
