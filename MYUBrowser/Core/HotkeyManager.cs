using System.Runtime.InteropServices;

namespace MYUBrowser.Core;

/// <summary>
/// 全局热键注册（Win32 RegisterHotKey）。自带一个 message-only 来接收 WM_HOTKEY，
/// 不受主窗体隐藏的影响。
/// </summary>
public sealed class HotkeyManager : NativeWindow, IDisposable
{
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    public const int WM_HOTKEY = 0x0312;
    public const int BOSS_KEY_ID = 0xB055;

    private static readonly IntPtr HWND_MESSAGE = new(-3);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private bool _registered;

    /// <summary>老板键被按下时触发</summary>
    public event Action? HotkeyPressed;

    public HotkeyManager()
    {
        // 创建一个 message-only 窗口，仅用于接收热键消息
        CreateHandle(new CreateParams { Parent = HWND_MESSAGE });
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && (int)m.WParam == BOSS_KEY_ID)
            HotkeyPressed?.Invoke();
        base.WndProc(ref m);
    }

    /// <summary>注册老板键，返回是否成功（与其他程序热键冲突则失败）</summary>
    public bool RegisterBossKey(uint modifiers, uint vk)
    {
        Unregister();
        _registered = RegisterHotKey(Handle, BOSS_KEY_ID, modifiers | MOD_NOREPEAT, vk);
        return _registered;
    }

    public void Unregister()
    {
        if (_registered)
        {
            UnregisterHotKey(Handle, BOSS_KEY_ID);
            _registered = false;
        }
    }

    public void Dispose()
    {
        Unregister();
        DestroyHandle();
    }

    /// <summary>把修饰键+键位转成可读文本</summary>
    public static string Describe(uint modifiers, uint vk)
    {
        var parts = new List<string>(4);
        if ((modifiers & MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((modifiers & MOD_ALT) != 0) parts.Add("Alt");
        if ((modifiers & MOD_SHIFT) != 0) parts.Add("Shift");
        if ((modifiers & MOD_WIN) != 0) parts.Add("Win");
        parts.Add(((Keys)vk).ToString());
        return string.Join(" + ", parts);
    }

    /// <summary>WinForms KeyEventArgs 转为 RegisterHotKey 需要的键位</summary>
    public static uint ToModifiers(Keys keyData)
    {
        uint m = 0;
        if ((keyData & Keys.Control) != 0) m |= MOD_CONTROL;
        if ((keyData & Keys.Alt) != 0) m |= MOD_ALT;
        if ((keyData & Keys.Shift) != 0) m |= MOD_SHIFT;
        return m;
    }
}
