using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using MYUBrowser.App;
using MYUBrowser.Core;
using MYUBrowser.UI;

namespace MYUBrowser.Views;

/// <summary>浏览器内容视图：地址栏、导航、书签、缩放、静音、页面伪装。作为可切换视图托管于 ShellForm。</summary>
public sealed class BrowserView : UserControl, IContentView, IShortcutHandler
{
    private const string DarkStyleId = "__myu_dark";
    private const string HideImgStyleId = "__myu_noimg";

    private readonly AppServices _services;
    private readonly AppSettings _settings;
    private readonly BookmarkStore _bookmarks;

    private readonly WebView2 _webView = new();
    private readonly ToolStrip _toolbar = new();
    private readonly ToolStripButton _btnBack = new("◀") { Enabled = false, ToolTipText = "后退" };
    private readonly ToolStripButton _btnForward = new("▶") { Enabled = false, ToolTipText = "前进" };
    private readonly ToolStripButton _btnRefresh = new("⟳") { ToolTipText = "刷新" };
    private readonly ToolStripButton _btnHome = new("⌂") { ToolTipText = "主页" };
    private readonly ToolStripTextBox _addressBox = new() { AutoSize = false, BorderStyle = BorderStyle.FixedSingle };
    private readonly ToolStripDropDownButton _btnBookmarks = new("★") { ToolTipText = "书签", ShowDropDownArrow = false };
    private readonly ToolStripButton _btnMute = new("♪") { ToolTipText = "静音开关", CheckOnClick = true };
    private readonly ToolStripButton _btnRandomFish = new("🎲") { ToolTipText = "随机摸鱼" };
    private readonly ToolStripLabel _zoomLabel = new("100%") { ToolTipText = "缩放（Ctrl+滚轮）" };

    private readonly Random _fishRandom = new();

    private bool _webReady;
    private bool _suspended;
    private string _title = "MYU Browser";
    private string? _hotkeyScriptId;

    public BrowserView(AppServices services)
    {
        _services = services;
        _settings = services.Settings;
        _bookmarks = services.Bookmarks;
        BuildUi();
        _services.SettingsApplied += ApplySettings;
    }

    public Control Control => this;
    public string Title => _title;
    public event Action<string?>? TitleChanged;

    // ================= UI =================

    private void BuildUi()
    {
        BackColor = Color.FromArgb(24, 24, 24);

        _toolbar.GripStyle = ToolStripGripStyle.Hidden;
        _toolbar.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable()) { RoundedEdges = false };
        _toolbar.Dock = DockStyle.Top;
        _toolbar.BackColor = Color.FromArgb(32, 32, 32);
        _toolbar.ForeColor = Color.Gainsboro;
        _toolbar.Padding = new Padding(6, 3, 6, 3);
        _toolbar.Font = new Font("Segoe UI", 9f);

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

        _btnBack.Click += (_, _) => { if (_webReady && _webView.CanGoBack) _webView.GoBack(); };
        _btnForward.Click += (_, _) => { if (_webReady && _webView.CanGoForward) _webView.GoForward(); };
        _btnRefresh.Click += (_, _) => { if (_webReady) _webView.Reload(); };
        _btnHome.Click += (_, _) => NavigateTo(_settings.HomeUrl);
        _btnBookmarks.DropDownOpening += (_, _) => RebuildBookmarkMenu();
        _btnBookmarks.DropDown = new ToolStripDropDownMenu();
        _btnMute.Checked = _settings.Muted;
        _btnMute.Click += (_, _) => SetMuted(_btnMute.Checked);
        _btnRandomFish.Click += (_, _) => OpenRandomFish();

        _toolbar.Items.AddRange(new ToolStripItem[]
        {
            _btnBack, _btnForward, _btnRefresh, _btnHome,
            _addressBox,
            _btnBookmarks, _btnMute, _btnRandomFish,
            new ToolStripSeparator(),
            _zoomLabel,
        });

        _webView.Dock = DockStyle.Fill;
        _webView.DefaultBackgroundColor = Color.FromArgb(24, 24, 24);

        Controls.Add(_webView);
        Controls.Add(_toolbar);

        Resize += (_, _) => LayoutAddressBox();
    }

    private void LayoutAddressBox()
    {
        int used = 0;
        foreach (ToolStripItem item in _toolbar.Items)
            if (item != _addressBox)
                used += item.Width + item.Margin.Horizontal;
        _addressBox.Width = Math.Max(120, _toolbar.DisplayRectangle.Width - used - 16);
    }

    // ================= IContentView =================

    public async Task InitializeAsync()
    {
        LayoutAddressBox();

        try
        {
            var env = await _services.GetWebViewEnvironmentAsync();
            await _webView.EnsureCoreWebView2Async(env);
        }
        catch (Exception ex)
        {
            MessageBox.Show(FindForm(),
                "WebView2 初始化失败，确认Microsoft Edge WebView2 Runtime状态\n\n" + ex.Message,
                "MYU Browser", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var core = _webView.CoreWebView2;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = true;
        core.Settings.AreBrowserAcceleratorKeysEnabled = true;
        core.Profile.PreferredColorScheme = CoreWebView2PreferredColorScheme.Dark;
        core.IsMuted = _settings.Muted;

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
        core.DocumentTitleChanged += (_, _) => SetTitle(core.DocumentTitle);
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

        _services.ClearCacheHandler = ClearCacheAsync;

        _webReady = true;
        await InjectHotkeyListenerAsync();
        NavigateTo(string.IsNullOrWhiteSpace(_settings.LastUrl) ? _settings.HomeUrl : _settings.LastUrl!);
    }

    /// <summary>按当前快捷键绑定（重新）注入网页内监听脚本。</summary>
    private async Task InjectHotkeyListenerAsync()
    {
        if (!_webReady || _services.Shortcuts == null) return;
        var core = _webView.CoreWebView2;
        string js = DisguiseScripts.BuildHotkeyListener(_services.Shortcuts.InPageBindings);
        try
        {
            if (_hotkeyScriptId != null)
                core.RemoveScriptToExecuteOnDocumentCreated(_hotkeyScriptId);
            _hotkeyScriptId = await core.AddScriptToExecuteOnDocumentCreatedAsync(js);
            await core.ExecuteScriptAsync(js);
        }
        catch { }
    }

    public void OnActivated()
    {
        if (_webReady && _suspended)
        {
            try { _webView.CoreWebView2.Resume(); } catch { }
            _suspended = false;
        }
        _webView.Focus();
    }

    public void OnSuspended()
    {
        if (_webReady && !_suspended)
        {
            try { _ = _webView.CoreWebView2.TrySuspendAsync(); } catch { }
            _suspended = true;
        }
    }

    public async Task OnHidingAsync()
    {
        if (!_webReady) return;
        try
        {
            await _webView.CoreWebView2.ExecuteScriptAsync(DisguiseScripts.PauseAllMedia);
            _webView.CoreWebView2.IsMuted = true;
        }
        catch { }
        try { await _webView.CoreWebView2.TrySuspendAsync(); _suspended = true; } catch { }
    }

    public void OnWoke()
    {
        if (!_webReady) return;
        if (_suspended)
        {
            try { _webView.CoreWebView2.Resume(); } catch { }
            _suspended = false;
        }
        try { _webView.CoreWebView2.IsMuted = _settings.Muted; } catch { }
    }

    // ================= 导航 / 书签 =================

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
        catch { }
    }

    private void OpenRandomFish()
    {
        if (_bookmarks.Items.Count == 0)
        {
            _services.ShowBalloon(2000, "MYU Browser",
                "还没有书签，先收藏几个网站再来摸鱼吧(现在还不支持随机推荐哦)", ToolTipIcon.Info);
            return;
        }
        int index = _fishRandom.Next(_bookmarks.Items.Count);
        NavigateTo(_bookmarks.Items[index].Url);
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

    // ================= 伪装 / 静音 / 缩放 / 设置 =================

    private void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string msg;
        try { msg = e.TryGetWebMessageAsString(); }
        catch { return; }

        const string prefix = "myu:cmd:";
        if (msg.StartsWith(prefix, StringComparison.Ordinal))
        {
            string id = msg[prefix.Length..];
            if (!ExecuteShortcut(id))
                _services.ExecuteShortcut(id);
        }
    }

    /// <summary>浏览器专属快捷键命令。</summary>
    public bool ExecuteShortcut(string commandId)
    {
        if (commandId == "address.focus")
        {
            _addressBox.Focus();
            _addressBox.SelectAll();
            return true;
        }
        return false;
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

    /// <summary>设置对话框确认后重新套用浏览器相关设置。</summary>
    private void ApplySettings()
    {
        _btnMute.Checked = _settings.Muted;
        SetMuted(_settings.Muted);
        if (_webReady)
            _webView.ZoomFactor = Math.Clamp(_settings.ZoomFactor, 0.25, 4.0);
        _ = ApplyPageStylesAsync();
        _ = InjectHotkeyListenerAsync();
    }

    private async Task ClearCacheAsync()
    {
        if (!_webReady) return;
        try { _webView.CoreWebView2.Resume(); } catch { }
        try
        {
            await _webView.CoreWebView2.Profile.ClearBrowsingDataAsync(
                CoreWebView2BrowsingDataKinds.DiskCache | CoreWebView2BrowsingDataKinds.CacheStorage);
        }
        catch { }
    }

    private void SetTitle(string? docTitle)
    {
        _title = string.IsNullOrWhiteSpace(docTitle) ? "MYU Browser" : docTitle!;
        TitleChanged?.Invoke(_title);
    }

    private static string Truncate(string s, int len) => s.Length <= len ? s : s[..len] + "…";
}
