using MYUBrowser.Core;

namespace MYUBrowser.Forms;

public sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;
    private readonly Func<long> _getCacheSize;
    private readonly Func<Task> _clearCache;

    private readonly TextBox _homeBox = new();
    private readonly TextBox _hotkeyBox = new() { ReadOnly = true };
    private readonly CheckBox _darkCheck = new() { Text = "强制深色（测试-反色滤镜，适配无深色模式的网站）", AutoSize = true };
    private readonly CheckBox _hideImgCheck = new() { Text = "淡化图片/视频（测试-页面更像纯文字）", AutoSize = true };
    private readonly CheckBox _fakeTitleCheck = new() { Text = "伪装窗口标题", AutoSize = true };
    private readonly TextBox _fakeTitleBox = new();
    private readonly CheckBox _closeToTrayCheck = new() { Text = "点关闭按钮时最小化到托盘（而不是退出）", AutoSize = true };
    private readonly CheckBox _autoDimCheck = new() { Text = "鼠标移出窗口时自动降低透明度到", AutoSize = true };
    private readonly NumericUpDown _autoDimValue = new() { Minimum = 0, Maximum = 100, Width = 60 };
    private readonly TrackBar _zoomTrack = new() { Minimum = 25, Maximum = 400, TickFrequency = 25, SmallChange = 5, LargeChange = 25, Width = 220, AutoSize = false, Height = 30 };
    private readonly NumericUpDown _zoomInput = new() { Minimum = 25, Maximum = 400, Increment = 5, Width = 70 };
    private readonly CheckBox _clearCacheCheck = new() { Text = "退出时自动清理磁盘缓存（保留登录状态）", AutoSize = true };
    private readonly NumericUpDown _cacheLimit = new() { Minimum = 10, Maximum = 4096, Increment = 50, Width = 80 };
    private readonly Label _cacheSizeLabel = new() { AutoSize = true };
    private readonly Button _clearNowBtn = new() { Text = "立即清理缓存", AutoSize = true };

    //检查更新
    private readonly Button _checkUpdateBtn = new() { Text = "检查新版本", AutoSize = true };
    private readonly Label _updateStatusLabel = new() { AutoSize = true };

    // 与窗体深色主题统一的文字色
    private static readonly Color TextPrimary = Color.Gainsboro;
    private static readonly Color TextMuted = Color.Gray;
    private static readonly Color TextSuccess = Color.FromArgb(130, 190, 130);
    private static readonly Color TextError = Color.FromArgb(220, 110, 110);

    private uint _recModifiers;
    private uint _recVk;

    public SettingsForm(AppSettings settings, Func<long> getCacheSize, Func<Task> clearCache)
    {
        _settings = settings;
        _getCacheSize = getCacheSize;
        _clearCache = clearCache;

        _recModifiers = settings.BossKeyModifiers;
        _recVk = settings.BossKeyVirtualKey;

        BuildUi();
        LoadValues();
    }

    private void BuildUi()
    {
        Text = "设置";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(37, 37, 38);
        ForeColor = Color.Gainsboro;
        Font = new Font("Segoe UI", 9f);

        this.AutoSize = true;
        this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        this.MinimumSize = new Size(450, 0);


        foreach (var tb in new[] { _homeBox, _hotkeyBox, _fakeTitleBox })
        {
            tb.BackColor = Color.FromArgb(56, 56, 56);
            tb.ForeColor = Color.Gainsboro;
            tb.BorderStyle = BorderStyle.FixedSingle;
        }
        foreach (var nu in new[] { _autoDimValue, _cacheLimit })
        {
            nu.BackColor = Color.FromArgb(56, 56, 56);
            nu.ForeColor = Color.Gainsboro;
        }

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Padding = new Padding(14),
            ColumnCount = 2,
            AutoSize = true,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        void AddRow(string label, Control ctrl)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var lb = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 7, 6, 7) };
            table.Controls.Add(lb, 0, row);
            ctrl.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            ctrl.Margin = new Padding(0, 4, 0, 4);
            table.Controls.Add(ctrl, 1, row);
        }

        void AddFullRow(Control ctrl)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            ctrl.Margin = new Padding(0, 4, 0, 4);
            table.Controls.Add(ctrl, 0, row);
            table.SetColumnSpan(ctrl, 2);
        }

        // 主页
        AddRow("主页", _homeBox);

        // 老板键录制
        _hotkeyBox.Cursor = Cursors.Hand;
        _hotkeyBox.GotFocus += (_, _) => _hotkeyBox.BackColor = Color.FromArgb(70, 70, 90);
        _hotkeyBox.LostFocus += (_, _) => _hotkeyBox.BackColor = Color.FromArgb(56, 56, 56);
        _hotkeyBox.KeyDown += OnHotkeyRecord;
        AddRow("老板键", _hotkeyBox);
        var hint = new Label
        {
            Text = "点击上方输入框后直接按组合键录制（需包含 Ctrl/Alt/Shift）",
            AutoSize = true,
            ForeColor = TextMuted,
        };
        AddFullRow(hint);

        // 网页缩放：滑动条 + 输入框联动
        _zoomTrack.BackColor = Color.FromArgb(37, 37, 38);
        _zoomTrack.Scroll += (_, _) => { if (_zoomInput.Value != _zoomTrack.Value) _zoomInput.Value = _zoomTrack.Value; };
        _zoomInput.ValueChanged += (_, _) => { if (_zoomTrack.Value != (int)_zoomInput.Value) _zoomTrack.Value = (int)_zoomInput.Value; };
        var zoomPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        zoomPanel.Controls.Add(_zoomTrack);
        zoomPanel.Controls.Add(_zoomInput);
        zoomPanel.Controls.Add(new Label { Text = "%", AutoSize = true, Margin = new Padding(2, 8, 0, 0) });
        AddRow("网页缩放", zoomPanel);

        // 页面伪装开关
        AddFullRow(_darkCheck);
        AddFullRow(_hideImgCheck);

        // 伪装标题
        var fakePanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        _fakeTitleCheck.Margin = new Padding(0, 3, 8, 0);
        _fakeTitleBox.Width = 200;
        fakePanel.Controls.Add(_fakeTitleCheck);
        fakePanel.Controls.Add(_fakeTitleBox);
        AddFullRow(fakePanel);

        AddFullRow(_closeToTrayCheck);

        // 自动降透明
        var dimPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        _autoDimCheck.Margin = new Padding(0, 3, 8, 0);
        dimPanel.Controls.Add(_autoDimCheck);
        dimPanel.Controls.Add(_autoDimValue);
        dimPanel.Controls.Add(new Label { Text = "%", AutoSize = true, Margin = new Padding(2, 5, 0, 0) });
        AddFullRow(dimPanel);

        AddFullRow(_clearCacheCheck);

        // 缓存上限
        var cachePanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        cachePanel.Controls.Add(new Label { Text = "磁盘缓存上限", AutoSize = true, Margin = new Padding(0, 5, 8, 0) });
        cachePanel.Controls.Add(_cacheLimit);
        cachePanel.Controls.Add(new Label { Text = "MB（重启后生效）", AutoSize = true, Margin = new Padding(4, 5, 0, 0) });
        AddFullRow(cachePanel);

        // 缓存占用 + 立即清理
        var cleanPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        _clearNowBtn.FlatStyle = FlatStyle.Flat;
        _clearNowBtn.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
        _clearNowBtn.Click += async (_, _) =>
        {
            _clearNowBtn.Enabled = false;
            await _clearCache();
            RefreshCacheSize();
            _clearNowBtn.Enabled = true;
        };
        _cacheSizeLabel.Margin = new Padding(8, 8, 0, 0);
        _cacheSizeLabel.ForeColor = TextPrimary;
        cleanPanel.Controls.Add(_clearNowBtn);
        cleanPanel.Controls.Add(_cacheSizeLabel);
        AddFullRow(cleanPanel);

        // 自动更新
        var updatePanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        _checkUpdateBtn.FlatStyle = FlatStyle.Flat;
        _checkUpdateBtn.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
        _updateStatusLabel.Margin = new Padding(8, 8, 0, 0);
        _updateStatusLabel.ForeColor = TextPrimary;
        _updateStatusLabel.Font = Font;

        _checkUpdateBtn.Click += async (_, _) =>
        {
            _checkUpdateBtn.Enabled = false;
            SetUpdateStatus("正在检查更新...", UpdateStatusKind.Loading);

            try
            {
                string repoUrl = _settings.UpdateRepoUrl?.Trim() ?? "";
                if (string.IsNullOrEmpty(repoUrl))
                {
                    SetUpdateStatus("未配置更新源", UpdateStatusKind.Error);
                    _checkUpdateBtn.Enabled = true;
                    return;
                }

                var source = new Velopack.Sources.GithubSource(repoUrl, null, _settings.UpdatePrerelease);
                var mgr = new Velopack.UpdateManager(source);

                var newVersion = await mgr.CheckForUpdatesAsync();
                if (newVersion == null)
                {
                    SetUpdateStatus("当前已是最新版本", UpdateStatusKind.Success);
                    _checkUpdateBtn.Enabled = true;
                    return;
                }

                SetUpdateStatus($"发现新版 {newVersion.TargetFullRelease.Version}，正在下载补丁…", UpdateStatusKind.Loading);
                await mgr.DownloadUpdatesAsync(newVersion);

                SetUpdateStatus("下载完成，即将重启应用…", UpdateStatusKind.Success);
                await Task.Delay(1500);

                mgr.ApplyUpdatesAndRestart(newVersion);
            }
            catch (Exception ex)
            {
                SetUpdateStatus($"检查更新失败：{ex.Message}", UpdateStatusKind.Error);
                _checkUpdateBtn.Enabled = true;
            }
        };

        updatePanel.Controls.Add(_checkUpdateBtn);
        updatePanel.Controls.Add(_updateStatusLabel);
        AddFullRow(updatePanel);

        // 确定 / 取消
        var btnPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Bottom,
            AutoSize = true,
            Padding = new Padding(0, 6, 14, 10),

            Margin = new Padding(0, 40, 0, 0)
        };
        var okBtn = new Button { Text = "确定", DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, AutoSize = true };
        var cancelBtn = new Button { Text = "取消", DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat, AutoSize = true };
        okBtn.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
        cancelBtn.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
        okBtn.Click += (_, _) => SaveValues();
        btnPanel.Controls.Add(cancelBtn);
        btnPanel.Controls.Add(okBtn);
        AcceptButton = okBtn;
        CancelButton = cancelBtn;

        Controls.Add(table);
        Controls.Add(btnPanel);
    }

    private void OnHotkeyRecord(object? sender, KeyEventArgs e)
    {
        e.SuppressKeyPress = true;
        e.Handled = true;

        // 忽略单独按下的修饰键
        if (e.KeyCode is Keys.ControlKey or Keys.Menu or Keys.ShiftKey or Keys.LWin or Keys.RWin) return;

        uint mods = HotkeyManager.ToModifiers(e.KeyData);
        if (mods == 0)
        {
            _hotkeyBox.Text = "必须包含 Ctrl / Alt / Shift 修饰键";
            return;
        }

        _recModifiers = mods;
        _recVk = (uint)e.KeyCode;
        _hotkeyBox.Text = HotkeyManager.Describe(_recModifiers, _recVk);
    }

    private void LoadValues()
    {
        _homeBox.Text = _settings.HomeUrl;
        _hotkeyBox.Text = HotkeyManager.Describe(_settings.BossKeyModifiers, _settings.BossKeyVirtualKey);
        int zoomPct = Math.Clamp((int)Math.Round(_settings.ZoomFactor * 100), 25, 400);
        _zoomInput.Value = zoomPct;
        _zoomTrack.Value = zoomPct;
        _darkCheck.Checked = _settings.ForceDarkFilter;
        _hideImgCheck.Checked = _settings.HideImages;
        _fakeTitleCheck.Checked = _settings.UseFakeTitle;
        _fakeTitleBox.Text = _settings.FakeTitle;
        _closeToTrayCheck.Checked = _settings.CloseToTray;
        _autoDimCheck.Checked = _settings.AutoDimOnMouseLeave;
        _autoDimValue.Value = Math.Clamp(_settings.AutoDimOpacityPercent, 0, 100);
        _clearCacheCheck.Checked = _settings.ClearCacheOnExit;
        _cacheLimit.Value = Math.Clamp(_settings.DiskCacheLimitMB, 10, 4096);
        RefreshCacheSize();
    }

    private void SaveValues()
    {
        if (!string.IsNullOrWhiteSpace(_homeBox.Text)) _settings.HomeUrl = _homeBox.Text.Trim();
        _settings.BossKeyModifiers = _recModifiers;
        _settings.BossKeyVirtualKey = _recVk;
        _settings.ZoomFactor = (double)_zoomInput.Value / 100.0;
        _settings.ForceDarkFilter = _darkCheck.Checked;
        _settings.HideImages = _hideImgCheck.Checked;
        _settings.UseFakeTitle = _fakeTitleCheck.Checked;
        if (!string.IsNullOrWhiteSpace(_fakeTitleBox.Text)) _settings.FakeTitle = _fakeTitleBox.Text.Trim();
        _settings.CloseToTray = _closeToTrayCheck.Checked;
        _settings.AutoDimOnMouseLeave = _autoDimCheck.Checked;
        _settings.AutoDimOpacityPercent = (int)_autoDimValue.Value;
        _settings.ClearCacheOnExit = _clearCacheCheck.Checked;
        _settings.DiskCacheLimitMB = (int)_cacheLimit.Value;
    }

    private void RefreshCacheSize()
    {
        double mb = _getCacheSize() / 1024.0 / 1024.0;
        _cacheSizeLabel.Text = $"当前数据目录占用：{mb:F1} MB";
    }

    private enum UpdateStatusKind { Normal, Loading, Success, Error }

    /// <summary>统一更新风格</summary>
    private void SetUpdateStatus(string text, UpdateStatusKind kind)
    {
        _updateStatusLabel.Text = text;
        _updateStatusLabel.ForeColor = kind switch
        {
            UpdateStatusKind.Loading => TextMuted,
            UpdateStatusKind.Success => TextSuccess,
            UpdateStatusKind.Error => TextError,
            _ => TextPrimary,
        };
    }
}
