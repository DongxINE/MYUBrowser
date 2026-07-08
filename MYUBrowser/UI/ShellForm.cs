using System.Runtime.InteropServices;
using MYUBrowser.App;

namespace MYUBrowser.UI;

/// <summary>
/// 唯一的隐身宿主窗口：提供无边框外壳、标题栏、全局透明度/极简模式、模式切换区，
/// 并在同一区域托管当前内容视图（切换时挂起上一个、激活目标）。
/// </summary>
public sealed class ShellForm : Form
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private const int WM_NCHITTEST = 0x0084;
    private const int WM_NCLBUTTONDOWN = 0x00A1;
    private const int HTCLIENT = 1, HTCAPTION = 2;
    private const int HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13,
        HTTOPRIGHT = 14, HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;
    private const int CS_DROPSHADOW = 0x00020000;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int ResizeBorder = 6;

    private readonly AppHost _host;

    private readonly Panel _contentPanel = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(24, 24, 24) };

    // ---- 标题栏 ----
    private readonly Panel _titleBar = new() { Dock = DockStyle.Top, Height = 26 };
    private readonly Label _titleLabel = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button _btnMinimize = new() { Text = "─", Dock = DockStyle.Right, Width = 40, FlatStyle = FlatStyle.Flat, TabStop = false };
    private readonly Button _btnClose = new() { Text = "✕", Dock = DockStyle.Right, Width = 40, FlatStyle = FlatStyle.Flat, TabStop = false };

    // ---- 全局控制条 ----
    private readonly ToolStrip _shellStrip = new();
    private readonly Dictionary<string, ToolStripButton> _modeButtons = new();
    private readonly ToolStripButton _btnMinimal = new("▁") { ToolTipText = "极简模式 (Ctrl+M)" };
    private readonly ToolStripButton _btnSettings = new("⚙") { ToolTipText = "当前模式设置" };
    private readonly TrackBar _opacityTrack = new()
    {
        Minimum = 0,
        Maximum = 100,
        TickStyle = TickStyle.None,
        AutoSize = false,
        Width = 90,
        Height = 22,
    };
    private readonly ToolStripLabel _opacityLabel = new("100%") { ToolTipText = "当前透明度" };

    private readonly System.Windows.Forms.Timer _autoDimTimer = new() { Interval = 400 };

    // ---- 视图托管 ----
    private readonly Dictionary<string, IContentView> _views = new();
    private IContentView? _active;
    private string _activeId = "";

    // ---- 状态 ----
    private bool _minimalMode;
    private bool _hiddenToTray;
    private bool _cacheCleanupDone;

    /// <summary>极简模式变化（供托盘“退出极简模式”项显隐）。</summary>
    public event Action<bool>? MinimalModeChanged;

    public ShellForm(AppHost host)
    {
        _host = host;
        BuildUi();
        ApplyWindowBounds();
        ApplyTitle(null);
        SetOpacity(_host.Settings.OpacityPercent, persist: false);
        TopMost = true;

        Load += (_, _) => ApplyRoundedCorners();
        FormClosing += OnFormClosing;
        _autoDimTimer.Tick += (_, _) => AutoDimTick();
        _autoDimTimer.Start();

        _host.Services.SettingsApplied += () => ApplyTitle(_active?.Title);
    }

    // ================= UI 构建 =================

    private void BuildUi()
    {
        Text = "MYU Browser";
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.FromArgb(24, 24, 24);
        Padding = new Padding(ResizeBorder);
        MinimumSize = new Size(360, 240);
        DoubleBuffered = true;
        KeyPreview = true;

        BuildTitleBar();
        BuildShellStrip();

        Controls.Add(_contentPanel);
        Controls.Add(_shellStrip);
        Controls.Add(_titleBar);
    }

    private void BuildTitleBar()
    {
        _titleBar.BackColor = Color.FromArgb(28, 28, 28);
        _titleLabel.ForeColor = Color.FromArgb(150, 150, 150);
        _titleLabel.Font = new Font("Segoe UI", 8.5f);
        _titleLabel.Padding = new Padding(8, 0, 0, 0);
        _titleBar.MouseDown += TitleBar_MouseDown;
        _titleLabel.MouseDown += TitleBar_MouseDown;
        _titleBar.MouseDoubleClick += (_, _) => ToggleMaximizeRestore();

        StyleCaptionButton(_btnMinimize, Color.FromArgb(55, 55, 55));
        StyleCaptionButton(_btnClose, Color.FromArgb(196, 43, 43));
        _btnMinimize.Click += (_, _) => _ = _host.HideToTrayAsync();
        _btnClose.Click += (_, _) =>
        {
            if (_host.Settings.CloseToTray) _ = _host.HideToTrayAsync();
            else _host.Exit();
        };

        _titleBar.Controls.Add(_titleLabel);
        _titleBar.Controls.Add(_btnMinimize);
        _titleBar.Controls.Add(_btnClose);
    }

    private void BuildShellStrip()
    {
        _shellStrip.GripStyle = ToolStripGripStyle.Hidden;
        _shellStrip.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable()) { RoundedEdges = false };
        _shellStrip.Dock = DockStyle.Top;
        _shellStrip.BackColor = Color.FromArgb(32, 32, 32);
        _shellStrip.ForeColor = Color.Gainsboro;
        _shellStrip.Padding = new Padding(6, 3, 6, 3);
        _shellStrip.Font = new Font("Segoe UI", 9f);
        _shellStrip.MouseDown += Strip_MouseDown;

        var items = new List<ToolStripItem>();

        // 模式切换按钮——遍历注册表生成
        foreach (var k in _host.Kinds)
        {
            var id = k.Id;
            var btn = new ToolStripButton(k.Glyph) { ToolTipText = k.Title };
            btn.Click += (_, _) => _host.SwitchTo(id);
            _modeButtons[id] = btn;
            items.Add(btn);
        }
        items.Add(new ToolStripSeparator());

        _btnMinimal.Click += (_, _) => ToggleMinimalMode();
        items.Add(_btnMinimal);

        _opacityTrack.Value = Math.Clamp(_host.Settings.OpacityPercent, 0, 100);
        _opacityTrack.BackColor = _shellStrip.BackColor;
        _opacityTrack.Scroll += (_, _) => SetOpacity(_opacityTrack.Value, persist: true);
        var opacityHost = new ToolStripControlHost(_opacityTrack) { ToolTipText = "整体透明度（Ctrl+↑/↓ 微调）" };
        items.Add(opacityHost);
        items.Add(_opacityLabel);

        _btnSettings.Click += (_, _) => _host.OpenSettingsFor(_activeId);
        items.Add(_btnSettings);

        _shellStrip.Items.AddRange(items.ToArray());
    }

    private static void StyleCaptionButton(Button b, Color hover)
    {
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = hover;
        b.FlatAppearance.MouseDownBackColor = hover;
        b.BackColor = Color.FromArgb(28, 28, 28);
        b.ForeColor = Color.Gainsboro;
        b.Font = new Font("Segoe UI", 9f);
    }

    // ================= 视图切换 =================

    public async void SwitchTo(string id)
    {
        var kind = _host.GetKind(id);
        if (kind == null) return;
        if (_activeId == id) { _active?.OnActivated(); return; }

        if (!_views.TryGetValue(id, out var view))
        {
            view = kind.Factory(_host.Services);
            _views[id] = view;
            var captured = view;
            view.Control.Dock = DockStyle.Fill;
            view.Control.Visible = false;
            view.TitleChanged += t => { if (ReferenceEquals(_active, captured)) ApplyTitle(t); };
            _contentPanel.Controls.Add(view.Control);
            try { await view.InitializeAsync(); } catch { }
        }

        if (_active != null)
        {
            _active.Control.Visible = false;
            _active.OnSuspended();
        }

        _active = view;
        _activeId = id;
        view.Control.Visible = true;
        view.Control.BringToFront();
        view.OnActivated();
        UpdateModeButtons(id);
        ApplyTitle(view.Title);
    }

    private void UpdateModeButtons(string activeId)
    {
        foreach (var (id, btn) in _modeButtons)
            btn.Checked = id == activeId;
    }

    // ================= 透明度 / 极简 / 伪装标题 =================

    public void SetOpacity(int percent, bool persist)
    {
        percent = Math.Clamp(percent, 0, 100);
        Opacity = percent / 100.0;
        if (_opacityTrack.Value != percent) _opacityTrack.Value = percent;
        _opacityLabel.Text = $"{percent}%";
        if (persist)
        {
            _host.Settings.OpacityPercent = percent;
            _host.Settings.Save();
        }
    }

    private void AutoDimTick()
    {
        var s = _host.Settings;
        if (!s.AutoDimOnMouseLeave || _hiddenToTray || !Visible) return;
        bool inside = Bounds.Contains(Cursor.Position);
        int target = inside ? s.OpacityPercent : Math.Min(s.OpacityPercent, s.AutoDimOpacityPercent);
        double d = target / 100.0;
        if (Math.Abs(Opacity - d) > 0.001) Opacity = d;
    }

    public void ToggleMinimalMode()
    {
        _minimalMode = !_minimalMode;
        _shellStrip.Visible = !_minimalMode;
        _titleBar.Visible = !_minimalMode;
        Padding = _minimalMode ? new Padding(0) : new Padding(ResizeBorder);
        MinimalModeChanged?.Invoke(_minimalMode);
    }

    public void ExitMinimalMode()
    {
        if (_minimalMode) ToggleMinimalMode();
    }

    private void ApplyTitle(string? viewTitle)
    {
        var s = _host.Settings;
        string title = s.UseFakeTitle
            ? s.FakeTitle
            : (string.IsNullOrWhiteSpace(viewTitle) ? "MYU Browser" : viewTitle!);
        Text = title;
        _titleLabel.Text = Truncate(title, 80);
        _host.SetTrayText(Truncate(title, 60));
    }

    private static string Truncate(string s, int len) => s.Length <= len ? s : s[..len] + "…";

    // ================= 老板键显隐 =================

    public async Task HideToTrayAsync()
    {
        if (_hiddenToTray) return;
        _hiddenToTray = true;
        if (_active != null)
        {
            try { await _active.OnHidingAsync(); } catch { }
        }
        Hide();
        ShowInTaskbar = false;
    }

    public void Wake(bool fullOpacity)
    {
        _hiddenToTray = false;
        if (fullOpacity) SetOpacity(100, persist: true);
        else SetOpacity(_host.Settings.OpacityPercent, persist: false);

        ShowInTaskbar = true;
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        TopMost = true;
        Activate();
        SetForegroundWindow(Handle);
        _active?.OnWoke();
        _active?.Control.Focus();
    }

    // ================= 无边框外壳 =================

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NCHITTEST && !_minimalMode && WindowState == FormWindowState.Normal)
        {
            base.WndProc(ref m);
            if ((int)m.Result == HTCLIENT)
            {
                var pos = PointToClient(new Point(m.LParam.ToInt32()));
                int hit = HitTestBorder(pos);
                if (hit != HTCLIENT) m.Result = new IntPtr(hit);
            }
            return;
        }
        base.WndProc(ref m);
    }

    private int HitTestBorder(Point p)
    {
        bool left = p.X <= ResizeBorder;
        bool right = p.X >= ClientSize.Width - ResizeBorder;
        bool top = p.Y <= ResizeBorder;
        bool bottom = p.Y >= ClientSize.Height - ResizeBorder;

        if (top && left) return HTTOPLEFT;
        if (top && right) return HTTOPRIGHT;
        if (bottom && left) return HTBOTTOMLEFT;
        if (bottom && right) return HTBOTTOMRIGHT;
        if (left) return HTLEFT;
        if (right) return HTRIGHT;
        if (top) return HTTOP;
        if (bottom) return HTBOTTOM;
        return HTCLIENT;
    }

    private void TitleBar_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, WM_NCLBUTTONDOWN, new IntPtr(HTCAPTION), IntPtr.Zero);
    }

    private void Strip_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        if (_shellStrip.GetItemAt(e.Location) != null) return;
        ReleaseCapture();
        SendMessage(Handle, WM_NCLBUTTONDOWN, new IntPtr(HTCAPTION), IntPtr.Zero);
    }

    private void ToggleMaximizeRestore()
    {
        WindowState = WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal
            : FormWindowState.Maximized;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= CS_DROPSHADOW;
            return cp;
        }
    }

    private void ApplyRoundedCorners()
    {
        try
        {
            int pref = 2; // DWMWCP_ROUND
            DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
        }
        catch { }
    }

    // ================= 快捷键 =================

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        var cmd = _host.Shortcuts.MatchLocal(keyData);
        if (cmd != null)
        {
            if (_active is IShortcutHandler h && h.ExecuteShortcut(cmd)) return true;
            if (_host.ExecuteShortcut(cmd)) return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // ================= 窗口边界 / 关闭 =================

    private void ApplyWindowBounds()
    {
        var s = _host.Settings;
        Size = new Size(s.WindowWidth, s.WindowHeight);
        if (s.WindowX >= 0 && s.WindowY >= 0 &&
            Screen.AllScreens.Any(sc => sc.WorkingArea.IntersectsWith(
                new Rectangle(s.WindowX, s.WindowY, s.WindowWidth, s.WindowHeight))))
        {
            StartPosition = FormStartPosition.Manual;
            Location = new Point(s.WindowX, s.WindowY);
        }
        else
        {
            StartPosition = FormStartPosition.CenterScreen;
        }
    }

    private async void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        var s = _host.Settings;

        if (e.CloseReason == CloseReason.UserClosing && s.CloseToTray && !_host.IsExiting)
        {
            e.Cancel = true;
            await _host.HideToTrayAsync();
            return;
        }

        if (WindowState == FormWindowState.Normal && Visible)
        {
            s.WindowX = Location.X;
            s.WindowY = Location.Y;
            s.WindowWidth = Size.Width;
            s.WindowHeight = Size.Height;
        }
        s.Save();

        if (s.ClearCacheOnExit && !_cacheCleanupDone && _host.Services.ClearCacheHandler != null)
        {
            e.Cancel = true;
            _cacheCleanupDone = true;
            try { await _host.Services.ClearCacheHandler!(); } catch { }
            _host.MarkExiting();
            Close();
            return;
        }

        _host.OnShellClosing();
    }
}
