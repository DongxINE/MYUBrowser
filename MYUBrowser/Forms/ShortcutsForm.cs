using MYUBrowser.Core;

namespace MYUBrowser.Forms;

/// <summary>统一的快捷键设置对话框：集中展示所有可自定义快捷键，点击录制、可恢复默认。</summary>
public sealed class ShortcutsForm : Form
{
    private static readonly Color TextPrimary = Color.Gainsboro;
    private static readonly Color TextMuted = Color.Gray;

    private readonly ShortcutManager _mgr;
    private readonly Dictionary<string, Keys> _working = new();
    private readonly Dictionary<string, TextBox> _boxes = new();

    public ShortcutsForm(ShortcutManager mgr)
    {
        _mgr = mgr;
        foreach (var c in mgr.Commands) _working[c.Id] = mgr.Get(c.Id);
        BuildUi();
    }

    private void BuildUi()
    {
        Text = "快捷键设置";
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
        MinimumSize = new Size(440, 0);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Padding = new Padding(14),
            ColumnCount = 2,
            AutoSize = true,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));

        void AddRow(string name, Control ctrl)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var lb = new Label { Text = name, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 8, 8), ForeColor = TextPrimary };
            table.Controls.Add(lb, 0, row);
            ctrl.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            ctrl.Margin = new Padding(0, 4, 0, 4);
            table.Controls.Add(ctrl, 1, row);
        }

        foreach (var c in _mgr.Commands)
        {
            var id = c.Id;
            var box = new TextBox
            {
                ReadOnly = true,
                Cursor = Cursors.Hand,
                BackColor = Color.FromArgb(56, 56, 56),
                ForeColor = Color.Gainsboro,
                BorderStyle = BorderStyle.FixedSingle,
                Text = ShortcutManager.Describe(_working[id]),
                TextAlign = HorizontalAlignment.Center,
            };
            box.GotFocus += (_, _) => box.BackColor = Color.FromArgb(70, 70, 90);
            box.LostFocus += (_, _) => box.BackColor = Color.FromArgb(56, 56, 56);
            box.KeyDown += (_, e) => OnRecord(id, box, e);
            _boxes[id] = box;
            AddRow(c.Name, box);
        }

        var tip = new Label
        {
            Text = "点击右侧方框后直接按组合键录制（需包含 Ctrl / Alt / Shift）",
            AutoSize = true,
            ForeColor = TextMuted,
            Margin = new Padding(0, 2, 0, 2),
        };
        int tipRow = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(tip, 0, tipRow);
        table.SetColumnSpan(tip, 2);

        var zoomNote = new Label
        {
            Text = "网页缩放：Ctrl + 鼠标滚轮（WebView 内置，不可更改）",
            AutoSize = true,
            ForeColor = TextMuted,
            Margin = new Padding(0, 2, 0, 2),
        };
        int noteRow = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(zoomNote, 0, noteRow);
        table.SetColumnSpan(zoomNote, 2);

        // 底部按钮
        var btnPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Bottom,
            AutoSize = true,
            Padding = new Padding(0, 6, 14, 10),
        };
        var okBtn = new Button { Text = "确定", DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, AutoSize = true };
        var cancelBtn = new Button { Text = "取消", DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat, AutoSize = true };
        var resetBtn = new Button { Text = "恢复默认", FlatStyle = FlatStyle.Flat, AutoSize = true };
        foreach (var b in new[] { okBtn, cancelBtn, resetBtn })
            b.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
        okBtn.Click += (_, _) => Apply();
        resetBtn.Click += (_, _) => ResetDefaults();
        btnPanel.Controls.Add(cancelBtn);
        btnPanel.Controls.Add(okBtn);
        btnPanel.Controls.Add(resetBtn);
        AcceptButton = okBtn;
        CancelButton = cancelBtn;

        Controls.Add(table);
        Controls.Add(btnPanel);
    }

    private void OnRecord(string id, TextBox box, KeyEventArgs e)
    {
        e.SuppressKeyPress = true;
        e.Handled = true;

        if (e.KeyCode is Keys.ControlKey or Keys.Menu or Keys.ShiftKey or Keys.LWin or Keys.RWin) return;

        if ((e.KeyData & Keys.Modifiers) == Keys.None)
        {
            box.Text = "需包含 Ctrl / Alt / Shift";
            return;
        }

        Keys gesture = e.KeyData;
        string? conflict = FindConflict(gesture, id);
        if (conflict != null)
        {
            box.Text = $"与「{conflict}」冲突";
            return;
        }

        _working[id] = gesture;
        box.Text = ShortcutManager.Describe(gesture);
    }

    private string? FindConflict(Keys gesture, string exceptId)
    {
        foreach (var (cid, cg) in _working)
            if (cid != exceptId && cg == gesture)
                return _mgr.Commands.First(c => c.Id == cid).Name;
        return null;
    }

    private void ResetDefaults()
    {
        foreach (var c in _mgr.Commands)
        {
            _working[c.Id] = c.DefaultGesture;
            _boxes[c.Id].Text = ShortcutManager.Describe(c.DefaultGesture);
        }
    }

    private void Apply()
    {
        foreach (var (id, gesture) in _working)
            _mgr.Set(id, gesture);
    }
}
