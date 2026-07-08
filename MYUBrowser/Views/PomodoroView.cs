using MYUBrowser.App;
using MYUBrowser.App.Features;
using MYUBrowser.Core;
using MYUBrowser.Forms;
using MYUBrowser.UI;

namespace MYUBrowser.Views;

/// <summary>
/// 番茄钟内容视图：计时器显示 + 控制按钮 + 待办清单。仅负责呈现与发送命令，
/// 计时/持久化逻辑全部在 PomodoroFeature，本视图只观察其事件刷新。
/// </summary>
public sealed class PomodoroView : UserControl, IContentView
{
    private static readonly Color Bg = Color.FromArgb(24, 24, 24);
    private static readonly Color Panel2 = Color.FromArgb(32, 32, 32);
    private static readonly Color RowBg = Color.FromArgb(40, 40, 40);
    private static readonly Color RowActive = Color.FromArgb(44, 60, 44);
    private static readonly Color Fg = Color.Gainsboro;
    private static readonly Color Dim = Color.FromArgb(150, 150, 150);
    private static readonly Color FocusColor = Color.FromArgb(0xE0, 0x5A, 0x47);
    private static readonly Color ShortColor = Color.FromArgb(0x4C, 0xAF, 0x50);
    private static readonly Color LongColor = Color.FromArgb(0x42, 0x9A, 0xE0);
    private static readonly Color OverdueColor = Color.FromArgb(0xE0, 0x6A, 0x6A);
    private static readonly Color SoonColor = Color.FromArgb(0xE0, 0xB0, 0x50);

    private readonly AppServices _services;
    private readonly PomodoroFeature _feature;

    private readonly Label _phaseLabel = new();
    private readonly Label _timeLabel = new();
    private readonly Label _cycleLabel = new();
    private readonly Button _btnPlay = new();
    private readonly Button _btnSkip = new();
    private readonly Button _btnReset = new();

    private readonly FlowLayoutPanel _taskFlow = new();
    private readonly TextBox _newTaskBox = new();
    private readonly NumericUpDown _estimate = new();
    private readonly DateTimePicker _dueAdd = new();
    private readonly Button _btnAdd = new();

    public PomodoroView(AppServices services, PomodoroFeature feature)
    {
        _services = services;
        _feature = feature;
        BuildUi();
        _feature.TimerChanged += OnTimerChanged;
        _feature.TodosChanged += OnTodosChanged;
    }

    public Control Control => this;
    public string Title => "番茄钟";
    public event Action<string?>? TitleChanged;

    public Task InitializeAsync()
    {
        UpdateTimer();
        RebuildTasks();
        return Task.CompletedTask;
    }

    public void OnActivated()
    {
        UpdateTimer();
        RebuildTasks();
    }

    public void OnSuspended() { }
    public Task OnHidingAsync() => Task.CompletedTask;
    public void OnWoke() => UpdateTimer();

    // ================= UI =================

    private void BuildUi()
    {
        BackColor = Bg;
        Font = new Font("Segoe UI", 9f);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            BackColor = Bg,
            Padding = new Padding(16, 12, 16, 12),
        };
        // 前四行按内容自适应高度（避免 DPI/字体缩放下固定像素导致重叠），列表占满剩余空间
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // phase
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // time
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // cycle
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // controls
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // task list
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // add row

        // 各标签 AutoSize + Anchor.None，由 TableLayoutPanel 自动水平居中
        _phaseLabel.AutoSize = true;
        _phaseLabel.Anchor = AnchorStyles.None;
        _phaseLabel.Font = new Font("Segoe UI Semibold", 14f);
        _phaseLabel.ForeColor = FocusColor;
        _phaseLabel.Margin = new Padding(0, 2, 0, 0);

        _timeLabel.AutoSize = true;
        _timeLabel.Anchor = AnchorStyles.None;
        _timeLabel.Font = new Font("Segoe UI", 44f, FontStyle.Bold);
        _timeLabel.ForeColor = Fg;
        _timeLabel.Margin = new Padding(0, 2, 0, 2);

        _cycleLabel.AutoSize = true;
        _cycleLabel.Anchor = AnchorStyles.None;
        _cycleLabel.ForeColor = Dim;
        _cycleLabel.Margin = new Padding(0, 0, 0, 6);

        var controls = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Anchor = AnchorStyles.None,
            Margin = new Padding(0, 0, 0, 8),
        };
        StylePillButton(_btnPlay, "开始", 92);
        StylePillButton(_btnSkip, "跳过", 76);
        StylePillButton(_btnReset, "重置", 76);
        _btnPlay.Click += (_, _) => _feature.TogglePlay();
        _btnSkip.Click += (_, _) => _feature.Skip();
        _btnReset.Click += (_, _) => _feature.Reset();
        controls.Controls.AddRange(new Control[] { _btnPlay, _btnSkip, _btnReset });

        _taskFlow.Dock = DockStyle.Fill;
        _taskFlow.FlowDirection = FlowDirection.TopDown;
        _taskFlow.WrapContents = false;
        _taskFlow.AutoScroll = true;
        _taskFlow.BackColor = Panel2;
        _taskFlow.Padding = new Padding(4);
        _taskFlow.Margin = new Padding(0, 6, 0, 6);
        _taskFlow.ClientSizeChanged += (_, _) => ResizeRows();

        var addRow = BuildAddRow();

        root.Controls.Add(_phaseLabel, 0, 0);
        root.Controls.Add(_timeLabel, 0, 1);
        root.Controls.Add(_cycleLabel, 0, 2);
        root.Controls.Add(controls, 0, 3);
        root.Controls.Add(_taskFlow, 0, 4);
        root.Controls.Add(addRow, 0, 5);

        Controls.Add(root);
    }

    private Control BuildAddRow()
    {
        var host = new Panel { Dock = DockStyle.Fill, BackColor = Bg };

        _newTaskBox.BorderStyle = BorderStyle.FixedSingle;
        _newTaskBox.BackColor = Color.FromArgb(56, 56, 56);
        _newTaskBox.ForeColor = Fg;
        _newTaskBox.Dock = DockStyle.Fill;
        _newTaskBox.Font = new Font("Segoe UI", 10f);
        _newTaskBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AddTask(); }
        };

        // 右侧一组控件用 FlowLayoutPanel 保证顺序确定：截止日期 · 🍅 · 预估 · 添加
        var right = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Bg,
            Padding = new Padding(6, 0, 0, 0),
        };

        _dueAdd.Format = DateTimePickerFormat.Custom;
        _dueAdd.CustomFormat = "MM-dd HH:mm";
        _dueAdd.ShowCheckBox = true;   // 勾选才表示设置了截止日期
        _dueAdd.Checked = false;
        _dueAdd.Width = 128;
        _dueAdd.Margin = new Padding(0, 8, 6, 0);
        _dueAdd.Value = DateTime.Now.Date.AddHours(18);

        var pomoTag = new Label
        {
            Text = "🍅",
            AutoSize = false,
            Width = 22,
            Height = 28,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Dim,
            Margin = new Padding(0, 6, 2, 0),
        };

        _estimate.Minimum = 1;
        _estimate.Maximum = 20;
        _estimate.Value = 1;
        _estimate.Width = 46;
        _estimate.BorderStyle = BorderStyle.FixedSingle;
        _estimate.BackColor = Color.FromArgb(56, 56, 56);
        _estimate.ForeColor = Fg;
        _estimate.TextAlign = HorizontalAlignment.Center;
        _estimate.Margin = new Padding(0, 8, 8, 0);

        StylePillButton(_btnAdd, "添加", 68);
        _btnAdd.Height = 28;
        _btnAdd.Margin = new Padding(0, 6, 0, 0);
        _btnAdd.Click += (_, _) => AddTask();

        right.Controls.Add(_dueAdd);
        right.Controls.Add(pomoTag);
        right.Controls.Add(_estimate);
        right.Controls.Add(_btnAdd);

        host.Controls.Add(_newTaskBox);
        host.Controls.Add(right);
        return host;
    }

    private static void StylePillButton(Button b, string text, int width)
    {
        b.Text = text;
        b.Width = width;
        b.Height = 34;
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        b.BackColor = Color.FromArgb(56, 56, 56);
        b.ForeColor = Color.Gainsboro;
        b.Font = new Font("Segoe UI", 10f);
        b.Margin = new Padding(6, 0, 6, 0);
        b.Cursor = Cursors.Hand;
    }

    private void AddTask()
    {
        var text = _newTaskBox.Text.Trim();
        if (text.Length == 0) return;
        DateTime? due = _dueAdd.Checked ? _dueAdd.Value : null;
        _feature.AddTask(text, (int)_estimate.Value, due);
        _newTaskBox.Clear();
        _estimate.Value = 1;
        _dueAdd.Checked = false;
        _newTaskBox.Focus();
    }

    private void EditTask(TodoTask task)
    {
        using var dlg = new TaskEditForm(task);
        if (_services.ShowModalDialog(dlg) == DialogResult.OK)
            _feature.EditTask(task.Id, dlg.TaskTitle, dlg.Estimate, dlg.Due, dlg.Done);
    }

    // ================= 刷新 =================

    private void OnTimerChanged()
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(UpdateTimer); return; }
        UpdateTimer();
    }

    private void OnTodosChanged()
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(RebuildTasks); return; }
        RebuildTasks();
    }

    private void UpdateTimer()
    {
        var t = _feature.Timer;
        int m = t.RemainingSeconds / 60, s = t.RemainingSeconds % 60;
        _timeLabel.Text = $"{m:00}:{s:00}";

        (_phaseLabel.Text, _phaseLabel.ForeColor) = t.Phase switch
        {
            PomodoroPhase.Focus => ("专注", FocusColor),
            PomodoroPhase.ShortBreak => ("短休息", ShortColor),
            PomodoroPhase.LongBreak => ("长休息", LongColor),
            _ => ("专注", FocusColor),
        };

        _btnPlay.Text = t.Running ? "暂停" : "开始";

        var active = ActiveTask();
        string taskText = active == null ? "未选择任务" : $"当前：{Truncate(active.Title, 16)}";
        _cycleLabel.Text = $"今日已完成 {t.CompletedFocus} 个番茄 · {taskText}";
    }

    private void RebuildTasks()
    {
        _taskFlow.SuspendLayout();
        _taskFlow.Controls.Clear();
        int lead = Math.Max(1, _services.Settings.TodoReminderLeadMinutes);
        foreach (var task in _feature.Todos.Items)
        {
            var row = new TaskRow(task, task.Id == _feature.ActiveTaskId, lead);
            row.ToggleClicked += r => _feature.ToggleTask(r.Task.Id);
            row.DeleteClicked += r => _feature.RemoveTask(r.Task.Id);
            row.ActiveClicked += r =>
                _feature.SetActiveTask(_feature.ActiveTaskId == r.Task.Id ? null : r.Task.Id);
            row.EditClicked += r => EditTask(r.Task);
            _taskFlow.Controls.Add(row);
        }
        _taskFlow.ResumeLayout();
        ResizeRows();
        UpdateTimer();
    }

    private void ResizeRows()
    {
        int w = _taskFlow.ClientSize.Width - _taskFlow.Padding.Horizontal - 4;
        foreach (Control c in _taskFlow.Controls)
            if (c is TaskRow r) r.Width = Math.Max(120, w);
    }

    private TodoTask? ActiveTask() =>
        _feature.Todos.Items.FirstOrDefault(t => t.Id == _feature.ActiveTaskId);

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _feature.TimerChanged -= OnTimerChanged;
            _feature.TodosChanged -= OnTodosChanged;
        }
        base.Dispose(disposing);
    }

    // ================= 单个待办行 =================

    private sealed class TaskRow : Panel
    {
        private readonly CheckBox _chk = new();
        private readonly Label _title = new();
        private readonly Label _due = new();
        private readonly Label _pomo = new();
        private readonly Button _edit = new();
        private readonly Button _active = new();
        private readonly Button _del = new();

        public TodoTask Task { get; }
        public event Action<TaskRow>? ToggleClicked;
        public event Action<TaskRow>? ActiveClicked;
        public event Action<TaskRow>? DeleteClicked;
        public event Action<TaskRow>? EditClicked;

        public TaskRow(TodoTask task, bool isActive, int leadMinutes)
        {
            Task = task;
            Height = 44;
            Margin = new Padding(0, 0, 0, 3);
            BackColor = isActive ? RowActive : RowBg;

            _chk.AutoSize = false;
            _chk.Checked = task.Done;
            _chk.CheckAlign = ContentAlignment.MiddleCenter;
            _chk.CheckedChanged += (_, _) => ToggleClicked?.Invoke(this);

            _title.Text = task.Title;
            _title.ForeColor = task.Done ? Dim : Fg;
            _title.Font = task.Done
                ? new Font("Segoe UI", 10f, FontStyle.Strikeout)
                : new Font("Segoe UI", 10f);
            _title.TextAlign = ContentAlignment.MiddleLeft;
            _title.AutoEllipsis = true;
            _title.Cursor = Cursors.Hand;
            _title.DoubleClick += (_, _) => EditClicked?.Invoke(this);

            _due.AutoEllipsis = true;
            _due.Font = new Font("Segoe UI", 8.5f);
            _due.TextAlign = ContentAlignment.MiddleLeft;
            ApplyDueStyle(task, leadMinutes);

            _pomo.Text = $"🍅 {task.CompletedPomodoros}/{task.EstimatePomodoros}";
            _pomo.ForeColor = Dim;
            _pomo.Font = new Font("Segoe UI", 8.5f);
            _pomo.TextAlign = ContentAlignment.MiddleCenter;

            StyleMini(_edit, "✎", Dim);
            _edit.Click += (_, _) => EditClicked?.Invoke(this);

            StyleMini(_active, isActive ? "★" : "☆", isActive ? LongColor : Dim);
            _active.Click += (_, _) => ActiveClicked?.Invoke(this);

            StyleMini(_del, "✕", Color.FromArgb(200, 120, 120));
            _del.Click += (_, _) => DeleteClicked?.Invoke(this);

            Controls.AddRange(new Control[] { _chk, _title, _due, _pomo, _edit, _active, _del });
        }

        private void ApplyDueStyle(TodoTask task, int leadMinutes)
        {
            if (task.DueDate == null)
            {
                _due.Text = "无截止";
                _due.ForeColor = Dim;
                return;
            }

            var due = task.DueDate.Value;
            _due.Text = due.ToString("MM-dd HH:mm");
            if (task.Done)
            {
                _due.ForeColor = Dim;
                return;
            }

            var now = DateTime.Now;
            if (due <= now)
                _due.ForeColor = OverdueColor;
            else if (due - now <= TimeSpan.FromMinutes(Math.Max(1, leadMinutes)))
                _due.ForeColor = SoonColor;
            else
                _due.ForeColor = Dim;
        }

        private static void StyleMini(Button b, string text, Color fore)
        {
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = Color.Transparent;
            b.ForeColor = fore;
            b.Font = new Font("Segoe UI", 11f);
            b.Cursor = Cursors.Hand;
            b.TabStop = false;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            int w = ClientSize.Width, h = ClientSize.Height;
            _chk.SetBounds(6, 0, 24, h);
            _del.SetBounds(w - 34, 0, 32, h);
            _active.SetBounds(w - 66, 0, 32, h);
            _edit.SetBounds(w - 98, 0, 32, h);
            _pomo.SetBounds(w - 164, 0, 62, h);
            int titleW = Math.Max(40, w - 164 - 44);
            _title.SetBounds(34, 4, titleW, 20);
            _due.SetBounds(34, 24, titleW, 16);
        }
    }
}
