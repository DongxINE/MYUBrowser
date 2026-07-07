using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using MYUBrowser.Core;
using MYUBrowser.Forms;

namespace MYUBrowser;

public sealed class MainForm : Form
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private void InitializeComponent()
    {

    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    // ---- 无边框窗口相关 Win32 常量 ----
    private const int WM_NCHITTEST = 0x0084;
    private const int WM_NCLBUTTONDOWN = 0x00A1;
    private const int HTCLIENT = 1, HTCAPTION = 2;
    private const int HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13,
        HTTOPRIGHT = 14, HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;
    private const int CS_DROPSHADOW = 0x00020000;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int ResizeBorder = 6;

    private const string DarkStyleId = "__myu_dark";
    private const string HideImgStyleId = "__myu_noimg";

    private readonly AppSettings _settings = AppSettings.Load();
    private readonly BookmarkStore _bookmarks = BookmarkStore.Load();
    private HotkeyManager? _hotkeys;

    // ---- 控件 ----
    private readonly WebView2 _webView = new();
    private readonly ToolStrip _toolbar = new();
    private readonly ToolStripButton _btnBack = new("◀") { Enabled = false, ToolTipText = "后退" };
    private readonly ToolStripButton _btnForward = new("▶") { Enabled = false, ToolTipText = "前进" };
    private readonly ToolStripButton _btnRefresh = new("⟳") { ToolTipText = "刷新" };
    private readonly ToolStripButton _btnHome = new("⌂") { ToolTipText = "主页" };
    private readonly ToolStripTextBox _addressBox = new() { AutoSize = false, BorderStyle = BorderStyle.FixedSingle };
    private readonly ToolStripDropDownButton _btnBookmarks = new("★") { ToolTipText = "书签", ShowDropDownArrow = false };
    private readonly ToolStripButton _btnMute = new("♪") { ToolTipText = "静音开关", CheckOnClick = true };
    private readonly ToolStripButton _btnMinimal = new("▁") { ToolTipText = "极简模式 (Ctrl+M)" };
    private readonly ToolStripLabel _zoomLabel = new("100%") { ToolTipText = "缩放（Ctrl+滚轮）" };
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
    private readonly ToolStripButton _btnSettings = new("⚙") { ToolTipText = "设置" };

    // ---- 顶部标题栏（拖拽区）----
    private readonly Panel _titleBar = new() { Dock = DockStyle.Top, Height = 26 };
    private readonly Label _titleLabel = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button _btnMinimize = new() { Text = "─", Dock = DockStyle.Right, Width = 40, FlatStyle = FlatStyle.Flat, TabStop = false };
    private readonly Button _btnClose = new() { Text = "✕", Dock = DockStyle.Right, Width = 40, FlatStyle = FlatStyle.Flat, TabStop = false };
    private readonly NotifyIcon _tray = new();
    private readonly System.Windows.Forms.Timer _autoDimTimer = new() { Interval = 400 };
    private Icon? _appIcon;

    // ---- 状态 ----
    private bool _hiddenToTray;
    private bool _exiting;
    private bool _cacheCleanupDone;
    private bool _minimalMode;
    private bool _webReady;

    public MainForm()
    {
        BuildUi();
        ApplyWindowBounds();
        ApplyTitle(null);
        SetOpacity(_settings.OpacityPercent, persist: false);
        TopMost = true; // 唤醒状态保持最高层级

        Load += async (_, _) => { ApplyRoundedCorners(); await InitAsync(); };
        FormClosing += OnFormClosing;
        Resize += (_, _) => LayoutAddressBox();
        _autoDimTimer.Tick += (_, _) => AutoDimTick();
        _autoDimTimer.Start();
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

        _toolbar.GripStyle = ToolStripGripStyle.Hidden;
        _toolbar.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable()) { RoundedEdges = false };
        _toolbar.Dock = DockStyle.Top;
        _toolbar.BackColor = Color.FromArgb(32, 32, 32);
        _toolbar.ForeColor = Color.Gainsboro;
        _toolbar.Padding = new Padding(6, 3, 6, 3);
        _toolbar.Font = new Font("Segoe UI", 9f);
        _toolbar.MouseDown += Toolbar_MouseDown;

        _addressBox.Font = new Font("Segoe UI", 9f);
        _addressBox.TextBox.BackColor = Color.FromArgb(56, 56, 56);
        _addressBox.TextBox.ForeColor = Color.Gainsboro;
        _addressBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                NavigateTo(_addressBox.Text);
            }
        };

        _opacityTrack.Value = Math.Clamp(_settings.OpacityPercent, 0, 100);
        _opacityTrack.BackColor = _toolbar.BackColor;
        _opacityTrack.Scroll += (_, _) => SetOpacity(_opacityTrack.Value, persist: true);
        var opacityHost = new ToolStripControlHost(_opacityTrack) { ToolTipText = "整体透明度（Ctrl+↑/↓ 微调）" };

        _btnBack.Click += (_, _) => { if (_webReady && _webView.CanGoBack) _webView.GoBack(); };
        _btnForward.Click += (_, _) => { if (_webReady && _webView.CanGoForward) _webView.GoForward(); };
        _btnRefresh.Click += (_, _) => { if (_webReady) _webView.Reload(); };
        _btnHome.Click += (_, _) => NavigateTo(_settings.HomeUrl);
        _btnBookmarks.DropDownOpening += (_, _) => RebuildBookmarkMenu();
        _btnBookmarks.DropDown = new ToolStripDropDownMenu();
        _btnMute.Checked = _settings.Muted;
        _btnMute.Click += (_, _) => SetMuted(_btnMute.Checked);
        _btnMinimal.Click += (_, _) => ToggleMinimalMode();
        _btnSettings.Click += (_, _) => OpenSettings();

        _toolbar.Items.AddRange(new ToolStripItem[]
        {
            _btnBack, _btnForward, _btnRefresh, _btnHome,
            _addressBox,
            _btnBookmarks, _btnMute, _btnMinimal,
            new ToolStripSeparator(),
            _zoomLabel, opacityHost, _opacityLabel, _btnSettings,
        });

        // 内容区容器
        _webView.Dock = DockStyle.Fill;
        _webView.DefaultBackgroundColor = Color.FromArgb(24, 24, 24);

        Controls.Add(_webView);
        Controls.Add(_toolbar);
        Controls.Add(_titleBar);

        // ---- 图标 ----
        _appIcon = CreateAppIcon();
        Icon = _appIcon;

        // ---- 托盘 ----
        _tray.Icon = _appIcon;
        _tray.Text = "MYU Browser";
        _tray.Visible = true;
        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("显示", null, (_, _) => Wake(fullOpacity: false));
        trayMenu.Items.Add("完全显示", null, (_, _) => Wake(fullOpacity: true));
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("退出", null, (_, _) => ExitApp());
        _tray.ContextMenuStrip = trayMenu;
        _tray.DoubleClick += (_, _) => Wake(fullOpacity: false);
    }

    /// <summary>程序内绘制图标</summary>
    private static Icon CreateAppIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            g.Clear(Color.Transparent);

            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            var rect = new Rectangle(2, 2, 27, 27);
            int r = 7;
            path.AddArc(rect.X, rect.Y, r, r, 180, 90);
            path.AddArc(rect.Right - r, rect.Y, r, r, 270, 90);
            path.AddArc(rect.Right - r, rect.Bottom - r, r, r, 0, 90);
            path.AddArc(rect.X, rect.Bottom - r, r, r, 90, 90);
            path.CloseFigure();

            using var bg = new SolidBrush(Color.FromArgb(46, 52, 64));
            g.FillPath(bg, path);
            using var pen = new Pen(Color.FromArgb(94, 129, 172), 1.5f);
            g.DrawPath(pen, path);

            using var font = new Font("Segoe UI", 15f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var fg = new SolidBrush(Color.FromArgb(216, 222, 233));
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("M", font, fg, new RectangleF(1, 0, 31, 32), sf);
        }

        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using var tmp = Icon.FromHandle(hIcon);
            return (Icon)tmp.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

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
        _btnMinimize.Click += (_, _) => _ = HideToTrayAsync();
        _btnClose.Click += (_, _) => { if (_settings.CloseToTray) _ = HideToTrayAsync(); else ExitApp(); };

        _titleBar.Controls.Add(_titleLabel);
        _titleBar.Controls.Add(_btnMinimize);
        _titleBar.Controls.Add(_btnClose);
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

    private void TitleBar_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, WM_NCLBUTTONDOWN, new IntPtr(HTCAPTION), IntPtr.Zero);
    }

    private void ToggleMaximizeRestore()
    {
        WindowState = WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal
            : FormWindowState.Maximized;
    }

    private void LayoutAddressBox()
    {
        int used = 0;
        foreach (ToolStripItem item in _toolbar.Items)
            if (item != _addressBox)
                used += item.Width + item.Margin.Horizontal;
        _addressBox.Width = Math.Max(120, _toolbar.DisplayRectangle.Width - used - 16);
    }

    private void ApplyWindowBounds()
    {
        Size = new Size(_settings.WindowWidth, _settings.WindowHeight);
        if (_settings.WindowX >= 0 && _settings.WindowY >= 0 &&
            Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(
                new Rectangle(_settings.WindowX, _settings.WindowY, _settings.WindowWidth, _settings.WindowHeight))))
        {
            StartPosition = FormStartPosition.Manual;
            Location = new Point(_settings.WindowX, _settings.WindowY);
        }
        else
        {
            StartPosition = FormStartPosition.CenterScreen;
        }
    }

    // ================= WebView 初始化 =================

    private async Task InitAsync()
    {
        LayoutAddressBox();
        RegisterBossKey();

        try
        {
            long cacheBytes = Math.Max(10, _settings.DiskCacheLimitMB) * 1024L * 1024L;
            var options = new CoreWebView2EnvironmentOptions
            {
                AdditionalBrowserArguments = $"--disk-cache-size={cacheBytes} --media-cache-size={cacheBytes}",
            };
            var env = await CoreWebView2Environment.CreateAsync(null, AppSettings.WebViewDataDir, options);
            await _webView.EnsureCoreWebView2Async(env);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "WebView2 初始化失败，确认Microsoft Edge WebView2 Runtime状态\n\n" + ex.Message,
                "MYU Browser", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var core = _webView.CoreWebView2;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = true;          // Ctrl+滚轮 缩放
        core.Settings.AreBrowserAcceleratorKeysEnabled = true;
        core.Profile.PreferredColorScheme = CoreWebView2PreferredColorScheme.Dark;
        core.IsMuted = _settings.Muted;

        await core.AddScriptToExecuteOnDocumentCreatedAsync(DisguiseScripts.HotkeyListener);

        core.SourceChanged += (_, _) =>
        {
            _addressBox.Text = core.Source;
            _settings.LastUrl = core.Source;
            _settings.Save();
        };
        core.HistoryChanged += (_, _) =>
        {
            _btnBack.Enabled = core.CanGoBack;
            _btnForward.Enabled = core.CanGoForward;
        };
        core.DocumentTitleChanged += (_, _) => ApplyTitle(core.DocumentTitle);
        core.NavigationCompleted += async (_, _) => await ApplyPageStylesAsync();
        core.WebMessageReceived += OnWebMessage;

        core.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;
            if (!string.IsNullOrEmpty(e.Uri))
                core.Navigate(e.Uri);
        };

        _webView.ZoomFactorChanged += (_, _) =>
        {
            _zoomLabel.Text = $"{(int)Math.Round(_webView.ZoomFactor * 100)}%";
            _settings.ZoomFactor = _webView.ZoomFactor;
            _settings.Save();
        };
        _webView.ZoomFactor = Math.Clamp(_settings.ZoomFactor, 0.25, 4.0);
        _zoomLabel.Text = $"{(int)Math.Round(_webView.ZoomFactor * 100)}%";

        _webReady = true;
        NavigateTo(string.IsNullOrWhiteSpace(_settings.LastUrl) ? _settings.HomeUrl : _settings.LastUrl!);
    }

    private void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string msg;
        try { msg = e.TryGetWebMessageAsString(); }
        catch { return; }

        switch (msg)
        {
            case "myu:opacity-up": SetOpacity(_settings.OpacityPercent + 5, persist: true); break;
            case "myu:opacity-down": SetOpacity(_settings.OpacityPercent - 5, persist: true); break;
            case "myu:minimal": ToggleMinimalMode(); break;
        }
    }

    // ================= 导航与书签 =================

    private void NavigateTo(string input)
    {
        if (!_webReady || string.IsNullOrWhiteSpace(input)) return;
        input = input.Trim();

        string url;
        if (Uri.TryCreate(input, UriKind.Absolute, out var abs) &&
            (abs.Scheme == Uri.UriSchemeHttp || abs.Scheme == Uri.UriSchemeHttps || abs.Scheme == "file"))
            url = abs.ToString();
        else if (!input.Contains(' ') && input.Contains('.'))
            url = "https://" + input;
        else
            url = string.Format(_settings.SearchUrlTemplate, Uri.EscapeDataString(input));

        try { _webView.CoreWebView2.Navigate(url); }
        catch { /* 非法 URL 忽略 */ }
    }

    private void RebuildBookmarkMenu()
    {
        var menu = _btnBookmarks.DropDown;
        menu.Items.Clear();

        string currentUrl = _webReady ? _webView.CoreWebView2.Source : "";
        bool bookmarked = !string.IsNullOrEmpty(currentUrl) && _bookmarks.Contains(currentUrl);
        var toggle = new ToolStripMenuItem(bookmarked ? "★ 取消收藏当前页" : "☆ 收藏当前页")
        {
            Enabled = _webReady && !string.IsNullOrEmpty(currentUrl),
        };
        toggle.Click += (_, _) =>
        {
            if (bookmarked) _bookmarks.Remove(currentUrl);
            else _bookmarks.Add(_webView.CoreWebView2.DocumentTitle, currentUrl);
        };
        menu.Items.Add(toggle);

        if (_bookmarks.Items.Count > 0)
        {
            menu.Items.Add(new ToolStripSeparator());
            foreach (var bm in _bookmarks.Items)
            {
                var item = new ToolStripMenuItem(Truncate(bm.Title, 48)) { ToolTipText = bm.Url };
                var url = bm.Url;
                item.Click += (_, _) => NavigateTo(url);
                menu.Items.Add(item);
            }

            menu.Items.Add(new ToolStripSeparator());
            var delMenu = new ToolStripMenuItem("删除书签…");
            foreach (var bm in _bookmarks.Items)
            {
                var del = new ToolStripMenuItem(Truncate(bm.Title, 48));
                var url = bm.Url;
                del.Click += (_, _) => _bookmarks.Remove(url);
                delMenu.DropDownItems.Add(del);
            }
            menu.Items.Add(delMenu);
        }
    }

    private static string Truncate(string s, int len) => s.Length <= len ? s : s[..len] + "…";

    // ================= 透明度 =================

    private void SetOpacity(int percent, bool persist)
    {
        percent = Math.Clamp(percent, 0, 100);
        Opacity = percent / 100.0;
        if (_opacityTrack.Value != percent) _opacityTrack.Value = percent;
        _opacityLabel.Text = $"{percent}%";
        if (persist)
        {
            _settings.OpacityPercent = percent;
            _settings.Save();
        }
    }

    private void AutoDimTick()
    {
        if (!_settings.AutoDimOnMouseLeave || _hiddenToTray || !Visible) return;
        bool inside = Bounds.Contains(Cursor.Position);
        int target = inside ? _settings.OpacityPercent
                            : Math.Min(_settings.OpacityPercent, _settings.AutoDimOpacityPercent);
        double d = target / 100.0;
        if (Math.Abs(Opacity - d) > 0.001) Opacity = d;
    }

    // ================= 老板键 / 托盘 =================

    private void RegisterBossKey()
    {
        if (_hotkeys == null)
        {
            _hotkeys = new HotkeyManager();
            _hotkeys.HotkeyPressed += OnBossKeyPressed;
        }
        if (!_hotkeys.RegisterBossKey(_settings.BossKeyModifiers, _settings.BossKeyVirtualKey))
        {
            _tray.ShowBalloonTip(3000, "MYU Browser",
                $"老板键 {HotkeyManager.Describe(_settings.BossKeyModifiers, _settings.BossKeyVirtualKey)} 注册失败（可能被其他程序占用），请在设置中更换。",
                ToolTipIcon.Warning);
        }
    }

    private void OnBossKeyPressed()
    {
        if (_hiddenToTray) Wake(fullOpacity: false);
        else _ = HideToTrayAsync();
    }

    protected override void WndProc(ref Message m)
    {
        // 无边框窗口：把窗口边缘映射为系统缩放热区，实现拖拽改变大小
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

    private void Toolbar_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        if (_toolbar.GetItemAt(e.Location) != null) return;
        ReleaseCapture();
        SendMessage(Handle, WM_NCLBUTTONDOWN, new IntPtr(HTCAPTION), IntPtr.Zero);
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

    private async Task HideToTrayAsync()
    {
        if (_hiddenToTray) return;
        _hiddenToTray = true;

        if (_webReady)
        {
            try
            {
                await _webView.CoreWebView2.ExecuteScriptAsync(DisguiseScripts.PauseAllMedia);
                _webView.CoreWebView2.IsMuted = true; // 兜底静音
            }
            catch { }
        }

        Hide();
        ShowInTaskbar = false;

        // 挂起渲染进程，降低内存占用
        if (_webReady)
        {
            try { await _webView.CoreWebView2.TrySuspendAsync(); } catch { }
        }
    }

    private void Wake(bool fullOpacity)
    {
        _hiddenToTray = false;

        if (_webReady)
        {
            try { _webView.CoreWebView2.Resume(); } catch { }
            try { _webView.CoreWebView2.IsMuted = _settings.Muted; } catch { }
        }

        if (fullOpacity) SetOpacity(100, persist: true);
        else SetOpacity(_settings.OpacityPercent, persist: false);

        ShowInTaskbar = true;
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        TopMost = true;

        // 夺取前台焦点，方便直接键盘操作
        Activate();
        SetForegroundWindow(Handle);
        _webView.Focus();
    }

    private void SetMuted(bool muted)
    {
        _settings.Muted = muted;
        _settings.Save();
        _btnMute.Checked = muted;
        _btnMute.Text = muted ? "🔇" : "♪";
        if (_webReady)
        {
            try { _webView.CoreWebView2.IsMuted = muted; } catch { }
        }
    }

    // ================= 伪装 =================

    private void ApplyTitle(string? docTitle)
    {
        string title = _settings.UseFakeTitle
            ? _settings.FakeTitle
            : (string.IsNullOrWhiteSpace(docTitle) ? "MYU Browser" : docTitle!);
        Text = title;
        _titleLabel.Text = Truncate(title, 80);
        _tray.Text = Truncate(title, 60);
    }

    private async Task ApplyPageStylesAsync()
    {
        if (!_webReady) return;
        try
        {
            var core = _webView.CoreWebView2;
            await core.ExecuteScriptAsync(DisguiseScripts.ToggleStyle(DarkStyleId, DisguiseScripts.DarkFilterCss, _settings.ForceDarkFilter));
            await core.ExecuteScriptAsync(DisguiseScripts.ToggleStyle(HideImgStyleId, DisguiseScripts.HideImagesCss, _settings.HideImages));
        }
        catch { }
    }

    private void ToggleMinimalMode()
    {
        _minimalMode = !_minimalMode;
        _toolbar.Visible = !_minimalMode;
        _titleBar.Visible = !_minimalMode;
        // 极简模式下边框一起去掉
        Padding = _minimalMode ? new Padding(0) : new Padding(ResizeBorder);
        if (!_minimalMode) LayoutAddressBox();
    }

    /// <summary>Win11 圆角</summary>
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
        switch (keyData)
        {
            case Keys.Control | Keys.Up: SetOpacity(_settings.OpacityPercent + 5, persist: true); return true;
            case Keys.Control | Keys.Down: SetOpacity(_settings.OpacityPercent - 5, persist: true); return true;
            case Keys.Control | Keys.M: ToggleMinimalMode(); return true;
            case Keys.Control | Keys.L:
                _addressBox.Focus();
                _addressBox.SelectAll();
                return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // ================= 设置 =================

    private void OpenSettings()
    {
        bool wasTopMost = TopMost;
        TopMost = false;

        using var dlg = new SettingsForm(_settings, GetCacheSizeBytes, ClearCacheNowAsync) { TopMost = true };
        var result = dlg.ShowDialog(this);

        TopMost = wasTopMost;

        if (result == DialogResult.OK)
        {
            _settings.Save();
            RegisterBossKey();
            _btnMute.Checked = _settings.Muted;
            SetMuted(_settings.Muted);
            if (_webReady)
                _webView.ZoomFactor = Math.Clamp(_settings.ZoomFactor, 0.25, 4.0);
            ApplyTitle(_webReady ? _webView.CoreWebView2.DocumentTitle : null);
            _ = ApplyPageStylesAsync();
        }
    }

    private static long GetCacheSizeBytes()
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

    private async Task ClearCacheNowAsync()
    {
        if (!_webReady) return;
        try
        {
            await _webView.CoreWebView2.Profile.ClearBrowsingDataAsync(
                CoreWebView2BrowsingDataKinds.DiskCache | CoreWebView2BrowsingDataKinds.CacheStorage);
        }
        catch { }
    }

    // ================= 关闭 =================

    private void ExitApp()
    {
        _exiting = true;
        Close();
    }

    private async void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        // 用户点 X 且设置为最小化到托盘
        if (e.CloseReason == CloseReason.UserClosing && _settings.CloseToTray && !_exiting)
        {
            e.Cancel = true;
            await HideToTrayAsync();
            return;
        }

        // 保存窗口位置与设置
        if (WindowState == FormWindowState.Normal && Visible)
        {
            _settings.WindowX = Location.X;
            _settings.WindowY = Location.Y;
            _settings.WindowWidth = Size.Width;
            _settings.WindowHeight = Size.Height;
        }
        _settings.Save();

        // 退出前清理磁盘缓存（保留 Cookie / 登录状态）
        if (_settings.ClearCacheOnExit && !_cacheCleanupDone && _webReady)
        {
            e.Cancel = true;
            _cacheCleanupDone = true;
            try { _webView.CoreWebView2.Resume(); } catch { }
            await ClearCacheNowAsync();
            _exiting = true;
            Close();
            return;
        }

        _hotkeys?.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _appIcon?.Dispose();
    }

    // ================= 深色工具栏配色 =================

    private sealed class DarkColorTable : ProfessionalColorTable
    {
        private static readonly Color Bg = Color.FromArgb(32, 32, 32);
        private static readonly Color Hover = Color.FromArgb(55, 55, 55);
        private static readonly Color Border = Color.FromArgb(64, 64, 64);

        public override Color ToolStripGradientBegin => Bg;
        public override Color ToolStripGradientMiddle => Bg;
        public override Color ToolStripGradientEnd => Bg;
        public override Color ToolStripBorder => Bg;
        public override Color ButtonSelectedHighlight => Hover;
        public override Color ButtonSelectedGradientBegin => Hover;
        public override Color ButtonSelectedGradientMiddle => Hover;
        public override Color ButtonSelectedGradientEnd => Hover;
        public override Color ButtonSelectedBorder => Border;
        public override Color ButtonPressedGradientBegin => Hover;
        public override Color ButtonPressedGradientMiddle => Hover;
        public override Color ButtonPressedGradientEnd => Hover;
        public override Color ButtonCheckedGradientBegin => Hover;
        public override Color ButtonCheckedGradientMiddle => Hover;
        public override Color ButtonCheckedGradientEnd => Hover;
        public override Color SeparatorDark => Border;
        public override Color SeparatorLight => Bg;
        public override Color MenuBorder => Border;
        public override Color ToolStripDropDownBackground => Bg;
        public override Color ImageMarginGradientBegin => Bg;
        public override Color ImageMarginGradientMiddle => Bg;
        public override Color ImageMarginGradientEnd => Bg;
        public override Color MenuItemSelected => Hover;
        public override Color MenuItemBorder => Border;
        public override Color MenuItemSelectedGradientBegin => Hover;
        public override Color MenuItemSelectedGradientEnd => Hover;
    }
}
