using MYUBrowser.App;
using MYUBrowser.Core;
using MYUBrowser.UI;

namespace MYUBrowser.Views;

/// <summary>
/// 本地阅读器视图：打开 txt/md/epub，自绘分页显示纯文本。支持字号/颜色自定义、
/// 方向键/WS/滚轮翻页、沉浸模式（隐藏工具栏，右下角悬浮按钮恢复）。透明度沿用整体透明度滑块。
/// </summary>
public sealed class ReaderView : UserControl, IContentView, IShortcutHandler
{
    private readonly AppServices _services;
    private readonly AppSettings _settings;

    private readonly ToolStrip _toolbar = new();
    private readonly ToolStripButton _btnOpen = new("📂 打开") { ToolTipText = "打开本地 txt / md / epub" };
    private readonly ToolStripButton _btnFontDown = new("A-") { ToolTipText = "减小字号" };
    private readonly ToolStripButton _btnFontUp = new("A+") { ToolTipText = "增大字号" };
    private readonly ToolStripButton _btnPrev = new("◀") { ToolTipText = "上一页 (↑ / W)" };
    private readonly ToolStripButton _btnNext = new("▶") { ToolTipText = "下一页 (↓ / S / 空格)" };
    private readonly ToolStripButton _btnImmersive = new("👓") { ToolTipText = "沉浸模式（隐藏工具栏）" };
    private readonly ToolStripLabel _pageLabel = new("—") { Alignment = ToolStripItemAlignment.Right };

    private readonly PagePanel _page;
    private readonly Button _btnRestore = new() { Text = "≡", Visible = false };

    private string _text = "";
    private string _title = "阅读器";
    private string? _currentFile;

    private List<string> _lines = new();
    private int[] _lineCharCum = Array.Empty<int>();  // 每行起始字符偏移的累计
    private int _linesPerPage = 1;
    private int _pageIndex;
    private Font _font;

    public ReaderView(AppServices services)
    {
        _services = services;
        _settings = services.Settings;
        _font = CreateFont(_settings.ReaderFontSize);
        _page = new PagePanel();
        BuildUi();
        _services.SettingsApplied += ApplySettings;
    }

    public Control Control => this;
    public string Title => _title;
    public event Action<string?>? TitleChanged;

    // ================= UI =================

    private void BuildUi()
    {
        BackColor = Color.FromArgb(_settings.ReaderBackColor);

        _toolbar.GripStyle = ToolStripGripStyle.Hidden;
        _toolbar.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable()) { RoundedEdges = false };
        _toolbar.Dock = DockStyle.Top;
        _toolbar.BackColor = Color.FromArgb(32, 32, 32);
        _toolbar.ForeColor = Color.Gainsboro;
        _toolbar.Padding = new Padding(6, 3, 6, 3);
        _toolbar.Font = new Font("Segoe UI", 9f);

        _btnOpen.Click += (_, _) => OpenFileDialog();
        _btnFontDown.Click += (_, _) => ChangeFontSize(-2);
        _btnFontUp.Click += (_, _) => ChangeFontSize(+2);
        _btnPrev.Click += (_, _) => Turn(-1);
        _btnNext.Click += (_, _) => Turn(+1);
        _btnImmersive.Click += (_, _) => SetImmersive(true);

        _toolbar.Items.AddRange(new ToolStripItem[]
        {
            _btnOpen, new ToolStripSeparator(),
            _btnFontDown, _btnFontUp, new ToolStripSeparator(),
            _btnPrev, _btnNext, new ToolStripSeparator(),
            _btnImmersive,
            _pageLabel,
        });

        _page.Dock = DockStyle.Fill;
        _page.BackColor = Color.FromArgb(_settings.ReaderBackColor);
        _page.Painter = DrawPage;
        _page.KeyDown += Page_KeyDown;
        _page.MouseWheel += Page_MouseWheel;
        _page.MouseDown += (_, _) => _page.Focus();
        _page.Resize += (_, _) => Repaginate();

        _btnRestore.SetBounds(0, 0, 40, 32);
        _btnRestore.FlatStyle = FlatStyle.Flat;
        _btnRestore.FlatAppearance.BorderSize = 0;
        _btnRestore.BackColor = Color.FromArgb(60, 60, 60);
        _btnRestore.ForeColor = Color.Gainsboro;
        _btnRestore.Cursor = Cursors.Hand;
        _btnRestore.TabStop = false;
        _btnRestore.Click += (_, _) => SetImmersive(false);

        Controls.Add(_page);
        Controls.Add(_toolbar);
        Controls.Add(_btnRestore);
        _btnRestore.BringToFront();

        Resize += (_, _) => PositionRestoreButton();
    }

    private void PositionRestoreButton()
    {
        _btnRestore.Location = new Point(ClientSize.Width - _btnRestore.Width - 14,
                                         ClientSize.Height - _btnRestore.Height - 14);
    }

    private static Font CreateFont(int size) =>
        new("Microsoft YaHei UI", Math.Clamp(size, 8, 60));

    // ================= IContentView =================

    public Task InitializeAsync()
    {
        if (!string.IsNullOrEmpty(_settings.ReaderLastFile) && File.Exists(_settings.ReaderLastFile))
            LoadFile(_settings.ReaderLastFile!, restorePosition: true);
        else
            Repaginate();
        return Task.CompletedTask;
    }

    public void OnActivated()
    {
        Repaginate();
        _page.Focus();
    }

    public void OnSuspended() => SavePosition();
    public Task OnHidingAsync() { SavePosition(); return Task.CompletedTask; }
    public void OnWoke() => _page.Focus();

    // ================= 文件加载 =================

    private void OpenFileDialog()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "打开文档",
            Filter = DocumentLoader.OpenFileDialogFilter,
        };
        if (_services.ShowFileDialog(dlg) == DialogResult.OK)
            LoadFile(dlg.FileName, restorePosition: false);
    }

    private void LoadFile(string path, bool restorePosition)
    {
        try
        {
            _text = DocumentLoader.Load(path);
        }
        catch (Exception ex)
        {
            _services.ShowBalloon(4000, "阅读器", "打开失败：" + ex.Message, ToolTipIcon.Error);
            return;
        }

        _currentFile = path;
        _title = Path.GetFileNameWithoutExtension(path);
        TitleChanged?.Invoke(_title);

        _settings.ReaderLastFile = path;
        if (!restorePosition) _settings.ReaderLastCharOffset = 0;
        _settings.Save();

        _pageIndex = 0;
        Repaginate();

        if (restorePosition)
            GoToOffset(_settings.ReaderLastCharOffset);
    }

    // ================= 分页 / 绘制 =================

    private void Repaginate()
    {
        int width = _page.ClientSize.Width - 2 * PagePanel.PadX;
        int usableHeight = _page.ClientSize.Height - 2 * PagePanel.PadY;
        if (width <= 10 || usableHeight <= 10) return;

        _lines = WrapLines(_text, _font, width);

        _lineCharCum = new int[_lines.Count + 1];
        for (int i = 0; i < _lines.Count; i++)
            _lineCharCum[i + 1] = _lineCharCum[i] + _lines[i].Length;

        int lineStep = _font.Height + _settings.ReaderLineSpacing;
        _linesPerPage = Math.Max(1, usableHeight / lineStep);

        _pageIndex = Math.Clamp(_pageIndex, 0, Math.Max(0, PageCount - 1));
        UpdatePageLabel();
        _page.Invalidate();
    }

    private int PageCount => _lines.Count == 0 ? 1 : (int)Math.Ceiling(_lines.Count / (double)_linesPerPage);

    /// <summary>按宽度对文本自动换行（二分测量，兼容中英文）。</summary>
    private static List<string> WrapLines(string text, Font font, int width)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(text)) return result;

        foreach (var paragraph in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            if (paragraph.Length == 0) { result.Add(""); continue; }

            int start = 0;
            while (start < paragraph.Length)
            {
                int remaining = paragraph.Length - start;
                int fit = 1, lo = 1, hi = remaining;
                while (lo <= hi)
                {
                    int mid = (lo + hi) / 2;
                    int w = TextRenderer.MeasureText(paragraph.Substring(start, mid), font,
                        new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width;
                    if (w <= width) { fit = mid; lo = mid + 1; }
                    else hi = mid - 1;
                }

                int len = fit;
                // 尽量在空格处断行（改善英文），中文无空格则按字符断
                if (start + len < paragraph.Length)
                {
                    int sp = paragraph.LastIndexOf(' ', start + len - 1, len);
                    if (sp > start) len = sp - start + 1;
                }
                result.Add(paragraph.Substring(start, len));
                start += len;
            }
        }
        return result;
    }

    private void DrawPage(PaintEventArgs e)
    {
        var g = e.Graphics;
        var color = Color.FromArgb(_settings.ReaderTextColor);

        if (_lines.Count == 0)
        {
            const string hint = "点击左上角 📂 打开本地 txt / md / epub 文档";
            TextRenderer.DrawText(g, hint, new Font("Microsoft YaHei UI", 11f),
                _page.ClientRectangle, Color.Gray,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        int lineStep = _font.Height + _settings.ReaderLineSpacing;
        int startLine = _pageIndex * _linesPerPage;
        int y = PagePanel.PadY;

        for (int i = startLine; i < startLine + _linesPerPage && i < _lines.Count; i++)
        {
            TextRenderer.DrawText(g, _lines[i], _font, new Point(PagePanel.PadX, y), color,
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            y += lineStep;
        }
    }

    private void UpdatePageLabel() => _pageLabel.Text = $"{_pageIndex + 1} / {PageCount}";

    // ================= 翻页 / 导航 =================

    private void Turn(int delta)
    {
        int target = Math.Clamp(_pageIndex + delta, 0, PageCount - 1);
        if (target == _pageIndex) return;
        _pageIndex = target;
        UpdatePageLabel();
        _page.Invalidate();
    }

    private void GoToOffset(int charOffset)
    {
        if (_lines.Count == 0) return;
        int line = 0;
        for (int i = 0; i < _lines.Count; i++)
        {
            if (_lineCharCum[i + 1] > charOffset) { line = i; break; }
            line = i;
        }
        _pageIndex = Math.Clamp(line / _linesPerPage, 0, PageCount - 1);
        UpdatePageLabel();
        _page.Invalidate();
    }

    private void SavePosition()
    {
        if (_currentFile == null || _lines.Count == 0) return;
        int startLine = Math.Clamp(_pageIndex * _linesPerPage, 0, _lines.Count);
        _settings.ReaderLastCharOffset = _lineCharCum[startLine];
        _settings.ReaderLastFile = _currentFile;
        _settings.Save();
    }

    private void ChangeFontSize(int delta)
    {
        int size = Math.Clamp(_settings.ReaderFontSize + delta, 8, 60);
        if (size == _settings.ReaderFontSize) return;

        int offset = CurrentOffset();
        _settings.ReaderFontSize = size;
        _settings.Save();

        _font.Dispose();
        _font = CreateFont(size);
        Repaginate();
        GoToOffset(offset);
    }

    private int CurrentOffset()
    {
        if (_lines.Count == 0) return 0;
        int startLine = Math.Clamp(_pageIndex * _linesPerPage, 0, _lines.Count);
        return _lineCharCum[startLine];
    }

    private void SetImmersive(bool on)
    {
        _toolbar.Visible = !on;
        _btnRestore.Visible = on;
        if (on) { PositionRestoreButton(); _btnRestore.BringToFront(); }
        _page.Focus();
    }

    // ================= 输入 =================

    private void Page_KeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Up or Keys.W or Keys.PageUp:
                Turn(-1); e.Handled = true; break;
            case Keys.Down or Keys.S or Keys.PageDown or Keys.Space:
                Turn(+1); e.Handled = true; break;
            case Keys.Home:
                _pageIndex = 0; UpdatePageLabel(); _page.Invalidate(); e.Handled = true; break;
            case Keys.End:
                _pageIndex = PageCount - 1; UpdatePageLabel(); _page.Invalidate(); e.Handled = true; break;
            case Keys.Escape when !_toolbar.Visible:
                SetImmersive(false); e.Handled = true; break;
        }
    }

    private void Page_MouseWheel(object? sender, MouseEventArgs e)
    {
        Turn(e.Delta > 0 ? -1 : +1);
    }

    // ================= IShortcutHandler =================

    public bool ExecuteShortcut(string commandId) => false;

    // ================= 设置联动 =================

    private void ApplySettings()
    {
        BackColor = Color.FromArgb(_settings.ReaderBackColor);
        _page.BackColor = Color.FromArgb(_settings.ReaderBackColor);
        int offset = CurrentOffset();
        _font.Dispose();
        _font = CreateFont(_settings.ReaderFontSize);
        Repaginate();
        GoToOffset(offset);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SavePosition();
            _services.SettingsApplied -= ApplySettings;
            _font.Dispose();
        }
        base.Dispose(disposing);
    }

    // ================= 自绘页面板 =================

    /// <summary>可获得焦点、双缓冲、把导航键当输入键处理的自绘面板。</summary>
    private sealed class PagePanel : Panel
    {
        public const int PadX = 40;
        public const int PadY = 28;

        public Action<PaintEventArgs>? Painter;

        public PagePanel()
        {
            SetStyle(ControlStyles.Selectable | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            TabStop = true;
        }

        protected override bool IsInputKey(Keys keyData) => keyData switch
        {
            Keys.Up or Keys.Down or Keys.Left or Keys.Right or
            Keys.PageUp or Keys.PageDown or Keys.Space or
            Keys.W or Keys.S or Keys.Home or Keys.End => true,
            _ => base.IsInputKey(keyData),
        };

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Painter?.Invoke(e);
        }
    }
}
