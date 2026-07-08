using MYUBrowser.Core;

namespace MYUBrowser.Forms;

/// <summary>番茄钟与待办相关设置（与浏览器设置分离）。</summary>
public sealed class PomodoroSettingsForm : Form
{
    private readonly AppSettings _settings;

    private readonly NumericUpDown _pomoFocus = new() { Minimum = 1, Maximum = 180, Width = 60 };
    private readonly NumericUpDown _pomoBreak = new() { Minimum = 1, Maximum = 60, Width = 60 };
    private readonly NumericUpDown _pomoLongBreak = new() { Minimum = 1, Maximum = 90, Width = 60 };
    private readonly NumericUpDown _pomoPerLong = new() { Minimum = 1, Maximum = 12, Width = 60 };
    private readonly CheckBox _pomoAutoStart = new() { Text = "阶段结束后自动开始下一段", AutoSize = true };
    private readonly CheckBox _pomoNotify = new() { Text = "阶段切换时弹托盘提醒", AutoSize = true };
    private readonly CheckBox _pomoDim = new() { Text = "专注时段自动把透明度降到", AutoSize = true };
    private readonly NumericUpDown _pomoDimValue = new() { Minimum = 0, Maximum = 100, Width = 60 };
    private readonly CheckBox _todoRemind = new() { Text = "临近截止时弹窗提醒", AutoSize = true };
    private readonly NumericUpDown _todoLead = new() { Minimum = 1, Maximum = 10080, Width = 72 };

    private static readonly Color TextMuted = Color.Gray;

    public PomodoroSettingsForm(AppSettings settings)
    {
        _settings = settings;
        BuildUi();
        LoadValues();
    }

    private void BuildUi()
    {
        Text = "番茄钟设置";
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
        MinimumSize = new Size(420, 0);

        foreach (var nu in new[] { _pomoFocus, _pomoBreak, _pomoLongBreak, _pomoPerLong, _pomoDimValue, _todoLead })
        {
            nu.BackColor = Color.FromArgb(56, 56, 56);
            nu.ForeColor = Color.Gainsboro;
        }

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Padding = new Padding(14),
            ColumnCount = 1,
            AutoSize = true,
        };

        void AddFullRow(Control ctrl)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            ctrl.Margin = new Padding(0, 4, 0, 4);
            table.Controls.Add(ctrl, 0, row);
        }

        AddFullRow(new Label
        {
            Text = "计时",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4),
        });

        var pomoTimes = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        void AddPomoField(string caption, Control c, string unit)
        {
            pomoTimes.Controls.Add(new Label { Text = caption, AutoSize = true, Margin = new Padding(0, 6, 4, 0) });
            pomoTimes.Controls.Add(c);
            pomoTimes.Controls.Add(new Label { Text = unit, AutoSize = true, Margin = new Padding(2, 6, 12, 0) });
        }
        AddPomoField("专注", _pomoFocus, "分");
        AddPomoField("短休", _pomoBreak, "分");
        AddPomoField("长休", _pomoLongBreak, "分");
        AddFullRow(pomoTimes);

        var pomoCycle = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        pomoCycle.Controls.Add(new Label { Text = "每完成", AutoSize = true, Margin = new Padding(0, 6, 4, 0) });
        pomoCycle.Controls.Add(_pomoPerLong);
        pomoCycle.Controls.Add(new Label { Text = "个专注后进入长休息", AutoSize = true, Margin = new Padding(4, 6, 0, 0) });
        AddFullRow(pomoCycle);

        AddFullRow(_pomoAutoStart);
        AddFullRow(_pomoNotify);

        var pomoDimPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        _pomoDim.Margin = new Padding(0, 3, 8, 0);
        pomoDimPanel.Controls.Add(_pomoDim);
        pomoDimPanel.Controls.Add(_pomoDimValue);
        pomoDimPanel.Controls.Add(new Label { Text = "%", AutoSize = true, Margin = new Padding(2, 5, 0, 0) });
        AddFullRow(pomoDimPanel);

        AddFullRow(new Label
        {
            Text = "待办提醒",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 10, 0, 4),
        });
        AddFullRow(_todoRemind);

        var leadPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        leadPanel.Controls.Add(new Label { Text = "截止前", AutoSize = true, Margin = new Padding(0, 6, 4, 0) });
        leadPanel.Controls.Add(_todoLead);
        leadPanel.Controls.Add(new Label { Text = "分钟内开始提醒", AutoSize = true, Margin = new Padding(4, 6, 0, 0) });
        AddFullRow(leadPanel);
        AddFullRow(new Label
        {
            Text = "每个任务仅提醒一次；老板键隐藏期间不弹窗，唤回后再提醒",
            AutoSize = true,
            ForeColor = TextMuted,
            MaximumSize = new Size(380, 0),
        });

        var btnPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Bottom,
            AutoSize = true,
            Padding = new Padding(0, 6, 14, 10),
            Margin = new Padding(0, 12, 0, 0),
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

    private void LoadValues()
    {
        _pomoFocus.Value = Math.Clamp(_settings.PomodoroMinutes, 1, 180);
        _pomoBreak.Value = Math.Clamp(_settings.PomodoroBreakMinutes, 1, 60);
        _pomoLongBreak.Value = Math.Clamp(_settings.PomodoroLongBreakMinutes, 1, 90);
        _pomoPerLong.Value = Math.Clamp(_settings.PomodorosPerLongBreak, 1, 12);
        _pomoAutoStart.Checked = _settings.PomodoroAutoStartNext;
        _pomoNotify.Checked = _settings.PomodoroNotify;
        _pomoDim.Checked = _settings.PomodoroDimOnFocus;
        _pomoDimValue.Value = Math.Clamp(_settings.PomodoroFocusDimOpacity, 0, 100);
        _todoRemind.Checked = _settings.TodoRemindEnabled;
        _todoLead.Value = Math.Clamp(_settings.TodoReminderLeadMinutes, 1, 10080);
    }

    private void SaveValues()
    {
        _settings.PomodoroMinutes = (int)_pomoFocus.Value;
        _settings.PomodoroBreakMinutes = (int)_pomoBreak.Value;
        _settings.PomodoroLongBreakMinutes = (int)_pomoLongBreak.Value;
        _settings.PomodorosPerLongBreak = (int)_pomoPerLong.Value;
        _settings.PomodoroAutoStartNext = _pomoAutoStart.Checked;
        _settings.PomodoroNotify = _pomoNotify.Checked;
        _settings.PomodoroDimOnFocus = _pomoDim.Checked;
        _settings.PomodoroFocusDimOpacity = (int)_pomoDimValue.Value;
        _settings.TodoRemindEnabled = _todoRemind.Checked;
        _settings.TodoReminderLeadMinutes = (int)_todoLead.Value;
    }
}
