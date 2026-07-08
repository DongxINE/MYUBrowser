using MYUBrowser.Core;

namespace MYUBrowser.Forms;

/// <summary>阅读器设置：字号、行距、文字色、背景色（与浏览器/番茄钟设置分离）。</summary>
public sealed class ReaderSettingsForm : Form
{
    private readonly AppSettings _settings;

    private readonly NumericUpDown _fontSize = new() { Minimum = 8, Maximum = 60, Width = 60 };
    private readonly NumericUpDown _lineSpacing = new() { Minimum = 0, Maximum = 40, Width = 60 };
    private readonly Button _textColorBtn = new() { Text = "选择…", FlatStyle = FlatStyle.Flat, Width = 80 };
    private readonly Button _backColorBtn = new() { Text = "选择…", FlatStyle = FlatStyle.Flat, Width = 80 };
    private readonly Panel _textColorSwatch = new() { Width = 28, Height = 20, BorderStyle = BorderStyle.FixedSingle };
    private readonly Panel _backColorSwatch = new() { Width = 28, Height = 20, BorderStyle = BorderStyle.FixedSingle };

    private Color _textColor;
    private Color _backColor;

    public ReaderSettingsForm(AppSettings settings)
    {
        _settings = settings;
        _textColor = Color.FromArgb(settings.ReaderTextColor);
        _backColor = Color.FromArgb(settings.ReaderBackColor);
        BuildUi();
        LoadValues();
    }

    private void BuildUi()
    {
        Text = "阅读器设置";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(37, 37, 38);
        ForeColor = Color.Gainsboro;
        Font = new Font("Segoe UI", 9f);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MinimumSize = new Size(360, 0);

        foreach (var nu in new[] { _fontSize, _lineSpacing })
        {
            nu.BackColor = Color.FromArgb(56, 56, 56);
            nu.ForeColor = Color.Gainsboro;
        }
        foreach (var b in new[] { _textColorBtn, _backColorBtn })
            b.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Padding = new Padding(14),
            ColumnCount = 2,
            AutoSize = true,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        void AddRow(string label, Control ctrl)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 7, 6, 7) }, 0, row);
            ctrl.Anchor = AnchorStyles.Left;
            ctrl.Margin = new Padding(0, 4, 0, 4);
            table.Controls.Add(ctrl, 1, row);
        }

        AddRow("字号", _fontSize);
        AddRow("行距(px)", _lineSpacing);

        _textColorBtn.Click += (_, _) => PickColor(ref _textColor, _textColorSwatch);
        var textPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        textPanel.Controls.Add(_textColorSwatch);
        _textColorBtn.Margin = new Padding(8, 0, 0, 0);
        textPanel.Controls.Add(_textColorBtn);
        AddRow("文字颜色", textPanel);

        _backColorBtn.Click += (_, _) => PickColor(ref _backColor, _backColorSwatch);
        var backPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        backPanel.Controls.Add(_backColorSwatch);
        _backColorBtn.Margin = new Padding(8, 0, 0, 0);
        backPanel.Controls.Add(_backColorBtn);
        AddRow("背景颜色", backPanel);

        var tip = new Label
        {
            Text = "整体透明度用顶部控制条的滑块调节；\n沉浸阅读点工具栏 👓，右下角 ≡ 可恢复。",
            AutoSize = true,
            ForeColor = Color.Gray,
            Margin = new Padding(0, 6, 0, 0),
        };
        int tipRow = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(tip, 0, tipRow);
        table.SetColumnSpan(tip, 2);

        var btnPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Bottom,
            AutoSize = true,
            Padding = new Padding(0, 6, 14, 10),
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

    private void PickColor(ref Color target, Panel swatch)
    {
        using var dlg = new ColorDialog { Color = target, FullOpen = true };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            target = dlg.Color;
            swatch.BackColor = target;
        }
    }

    private void LoadValues()
    {
        _fontSize.Value = Math.Clamp(_settings.ReaderFontSize, 8, 60);
        _lineSpacing.Value = Math.Clamp(_settings.ReaderLineSpacing, 0, 40);
        _textColorSwatch.BackColor = _textColor;
        _backColorSwatch.BackColor = _backColor;
    }

    private void SaveValues()
    {
        _settings.ReaderFontSize = (int)_fontSize.Value;
        _settings.ReaderLineSpacing = (int)_lineSpacing.Value;
        _settings.ReaderTextColor = _textColor.ToArgb();
        _settings.ReaderBackColor = _backColor.ToArgb();
    }
}
