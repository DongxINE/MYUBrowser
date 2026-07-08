using MYUBrowser.Core;
using MYUBrowser.Forms;
using MYUBrowser.UI;

namespace MYUBrowser.App;

/// <summary>
/// 应用协调器（单一入口）。拥有托盘、老板键、共享设置/图标、内容界面注册表与横切功能列表，
/// 并对外暴露事件中枢供 IAppFeature 订阅。任意时刻仅驱动一个 ShellForm 窗口。
/// </summary>
public sealed class AppHost : IDisposable
{
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly BookmarkStore _bookmarks = BookmarkStore.Load();
    private readonly ShortcutManager _shortcuts;
    private readonly AppServices _services;

    private readonly List<ContentKind> _kinds = new();
    private readonly Dictionary<string, ContentKind> _kindMap = new();
    private readonly List<IAppFeature> _features = new();

    private readonly NotifyIcon _tray = new();
    private readonly ToolStripMenuItem _trayExitMinimal = new("退出极简模式") { Visible = false };
    private readonly System.Windows.Forms.Timer _tick = new() { Interval = 1000 };

    private Icon? _icon;
    private HotkeyManager? _hotkeys;
    private ShellForm? _shell;
    private string _defaultViewId = "";
    private bool _hidden;
    private bool _exiting;

    public AppHost()
    {
        _shortcuts = new ShortcutManager(_settings);
        _services = new AppServices(_settings, _bookmarks)
        {
            RequestSwitchTo = SwitchTo,
            ShowBalloon = (ms, title, text, icon) => _tray.ShowBalloonTip(ms, title, text, icon),
            ExecuteShortcut = ExecuteShortcut,
            Shortcuts = _shortcuts,
            ShowModalDialog = ShowModal,
        };
    }

    /// <summary>窗口是否已被老板键隐藏。</summary>
    public bool IsHidden => _hidden;

    /// <summary>以 Shell 为父窗口安全地弹出模态对话框：临时解除 TopMost，避免被主窗盖住形成死锁。</summary>
    public DialogResult ShowModal(Form dlg)
    {
        if (_shell == null) return dlg.ShowDialog();
        bool wasTopMost = _shell.TopMost;
        _shell.TopMost = false;
        dlg.TopMost = true;
        var result = dlg.ShowDialog(_shell);
        _shell.TopMost = wasTopMost;
        return result;
    }

    // ---- 事件中枢（供 IAppFeature 订阅）----
    public event Action? SecondTick;
    public event Action<string>? ContentSwitched;
    public event Action? WindowsHiding;
    public event Action? WindowsWoke;

    public AppSettings Settings => _settings;
    public AppServices Services => _services;
    public ShortcutManager Shortcuts => _shortcuts;
    public IReadOnlyList<ContentKind> Kinds => _kinds;
    public bool IsExiting => _exiting;

    /// <summary>执行全局快捷键命令（透明度/极简等）。</summary>
    public bool ExecuteShortcut(string id)
    {
        switch (id)
        {
            case "opacity.up": _shell?.SetOpacity(_settings.OpacityPercent + 5, persist: true); return true;
            case "opacity.down": _shell?.SetOpacity(_settings.OpacityPercent - 5, persist: true); return true;
            case "minimal.toggle": _shell?.ToggleMinimalMode(); return true;
        }
        return false;
    }

    /// <summary>供功能设置全局透明度（如番茄专注期降透）。不落盘，仅临时调整。</summary>
    public void SetOpacity(int percent, bool persist) => _shell?.SetOpacity(percent, persist);

    /// <summary>当前设置里的用户目标透明度（用于专注结束后恢复）。</summary>
    public int UserOpacity => _settings.OpacityPercent;

    public ContentKind? GetKind(string id) => _kindMap.TryGetValue(id, out var k) ? k : null;

    public void RegisterView(ContentKind kind)
    {
        if (_kindMap.ContainsKey(kind.Id)) return;
        _kinds.Add(kind);
        _kindMap[kind.Id] = kind;
    }

    public void AddFeature(IAppFeature feature) => _features.Add(feature);

    /// <summary>启动应用：创建窗口、装配托盘/老板键/功能，进入消息循环。</summary>
    public void Run(string defaultViewId)
    {
        _defaultViewId = defaultViewId;
        _icon = AppIconFactory.Create();

        _shell = new ShellForm(this) { Icon = _icon };
        _shell.MinimalModeChanged += m => _trayExitMinimal.Visible = m;
        _shell.Load += (_, _) => SwitchTo(_defaultViewId);

        BuildTray();
        RegisterBossKey();

        foreach (var f in _features) f.Attach(this);

        _tick.Tick += (_, _) => SecondTick?.Invoke();
        _tick.Start();

        System.Windows.Forms.Application.Run(_shell);
    }

    public void SwitchTo(string id)
    {
        if (!_kindMap.ContainsKey(id)) return;
        _shell?.SwitchTo(id);
        ContentSwitched?.Invoke(id);
    }

    /// <summary>更新托盘悬浮文字（跟随伪装标题）。</summary>
    public void SetTrayText(string text) => _tray.Text = text;

    // ================= 托盘 =================

    private void BuildTray()
    {
        _tray.Icon = _icon;
        _tray.Text = "MYU Browser";
        _tray.Visible = true;

        var menu = new ContextMenuStrip();
        menu.Items.Add("显示", null, (_, _) => Wake(fullOpacity: false));
        menu.Items.Add("完全显示", null, (_, _) => Wake(fullOpacity: true));
        menu.Items.Add(new ToolStripSeparator());

        // “切换到” 子菜单——遍历注册表动态生成
        var switchMenu = new ToolStripMenuItem("切换到");
        foreach (var k in _kinds)
        {
            var id = k.Id;
            switchMenu.DropDownItems.Add($"{k.Glyph} {k.Title}", null, (_, _) =>
            {
                Wake(fullOpacity: false);
                SwitchTo(id);
            });
        }
        menu.Items.Add(switchMenu);

        _trayExitMinimal.Click += (_, _) => _shell?.ExitMinimalMode();
        menu.Items.Add(_trayExitMinimal);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => Exit());

        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => Wake(fullOpacity: false);
    }

    // ================= 老板键 / 显隐 =================

    private void RegisterBossKey()
    {
        if (_hotkeys == null)
        {
            _hotkeys = new HotkeyManager();
            _hotkeys.HotkeyPressed += OnBossKeyPressed;
        }
        if (!_hotkeys.RegisterBossKey(_shortcuts.BossModifiers, _shortcuts.BossVirtualKey))
        {
            _tray.ShowBalloonTip(3000, "MYU Browser",
                $"老板键 {ShortcutManager.Describe(_shortcuts.Get("boss.toggle"))} 注册失败（可能被其他程序占用），请在快捷键设置中更换。",
                ToolTipIcon.Warning);
        }
    }

    private void OnBossKeyPressed()
    {
        if (_hidden) Wake(fullOpacity: false);
        else _ = HideToTrayAsync();
    }

    public async Task HideToTrayAsync()
    {
        if (_hidden || _shell == null) return;
        _hidden = true;
        WindowsHiding?.Invoke();
        await _shell.HideToTrayAsync();
    }

    public void Wake(bool fullOpacity)
    {
        if (_shell == null) return;
        _hidden = false;
        _shell.Wake(fullOpacity);
        WindowsWoke?.Invoke();
    }

    // ================= 设置 =================

    public void OpenSettings()
    {
        if (_shell == null) return;

        using var dlg = new SettingsForm(
            _settings,
            AppServices.GetCacheSizeBytes,
            () => _services.ClearCacheHandler?.Invoke() ?? Task.CompletedTask,
            OpenShortcuts);

        if (ShowModal(dlg) == DialogResult.OK)
        {
            _settings.Save();
            RegisterBossKey();
            _services.NotifySettingsApplied();
        }
    }

    /// <summary>打开番茄钟 / 待办相关设置（与浏览器设置分离）。</summary>
    public void OpenPomodoroSettings()
    {
        if (_shell == null) return;

        using var dlg = new PomodoroSettingsForm(_settings);
        if (ShowModal(dlg) == DialogResult.OK)
        {
            _settings.Save();
            _services.NotifySettingsApplied();
        }
    }

    /// <summary>按当前活动界面打开对应设置页。</summary>
    public void OpenSettingsFor(string? activeViewId)
    {
        if (activeViewId == "pomodoro") OpenPomodoroSettings();
        else OpenSettings();
    }

    /// <summary>打开统一的快捷键设置对话框。</summary>
    public void OpenShortcuts(IWin32Window owner)
    {
        using var dlg = new ShortcutsForm(_shortcuts) { TopMost = true };
        if (dlg.ShowDialog(owner) == DialogResult.OK)
        {
            _shortcuts.Save();
            RegisterBossKey();
            _services.NotifySettingsApplied();
        }
    }

    // ================= 退出 =================

    public void Exit()
    {
        _exiting = true;
        _shell?.Close();
    }

    public void MarkExiting() => _exiting = true;

    /// <summary>由 ShellForm 在真正关闭前调用，统一清理应用级资源。</summary>
    public void OnShellClosing()
    {
        _tick.Stop();
        _tick.Dispose();
        foreach (var f in _features) f.Dispose();
        _hotkeys?.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _icon?.Dispose();
    }

    public void Dispose() => OnShellClosing();
}
