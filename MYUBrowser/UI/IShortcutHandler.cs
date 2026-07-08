namespace MYUBrowser.UI;

/// <summary>
/// 内容视图可选实现：处理某些视图专属的快捷键命令（如浏览器的“聚焦地址栏”）。
/// ShellForm 分发命令时会先询问当前视图，未处理再交给全局。
/// </summary>
public interface IShortcutHandler
{
    /// <summary>尝试执行命令，处理了返回 true。</summary>
    bool ExecuteShortcut(string commandId);
}
