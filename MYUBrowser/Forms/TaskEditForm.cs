using MYUBrowser.Core;

namespace MYUBrowser.Forms;

/// <summary>编辑单个待办：标题、预估番茄、截止日期（可无）、是否完成。</summary>
public sealed class TaskEditForm : Form
{
    private readonly TextBox _title = new();
    private readonly NumericUpDown _estimate = new() { Minimum = 1, Maximum = 20 };
    private readonly CheckBox _hasDue = new() { Text = "设置截止日期", AutoSize = true };
    private readonly DateTimePicker _due = new()
    {
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "yyyy-MM-dd  HH:mm",
        ShowUpDown = false,
    };
    private readonly CheckBox _done = new() { Text = "已完成", AutoSize = true };

    public string TaskTitle => _title.Text.Trim();
    public int Estimate => (int)_estimate.Value;
    public DateTime? Due => _hasDue.Checked ? _due.Value : null;
    public bool Done => _done.Checked;

    public TaskEditForm(TodoTask task)
    {
        Text = "编辑待办";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(37, 37, 38);
        ForeColor = Color.Gainsboro;
        Font = new Font("Segoe UI", 9f);
        ClientSize = new Size(360, 210);

        _title.Text = task.Title;
        _title.BackColor = Color.FromArgb(56, 56, 56);
        _title.ForeColor = Color.Gainsboro;
        _title.BorderStyle = BorderStyle.FixedSingle;
        _title.SetBounds(16, 32, 328, 24);

        _estimate.BackColor = Color.FromArgb(56, 56, 56);
        _estimate.ForeColor = Color.Gainsboro;
        _estimate.Value = Math.Clamp(task.EstimatePomodoros, 1, 20);
        _estimate.SetBounds(120, 68, 60, 24);

        _hasDue.SetBounds(16, 106, 130, 22);
        _hasDue.Checked = task.DueDate != null;
        _hasDue.CheckedChanged += (_, _) => _due.Enabled = _hasDue.Checked;

        _due.SetBounds(150, 104, 194, 24);
        _due.Value = task.DueDate ?? DateTime.Now.Date.AddHours(18);
        _due.Enabled = _hasDue.Checked;

        _done.SetBounds(16, 140, 100, 22);
        _done.Checked = task.Done;

        var okBtn = new Button { Text = "确定", DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat };
        var cancelBtn = new Button { Text = "取消", DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat };
        okBtn.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
        cancelBtn.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
        okBtn.SetBounds(176, 172, 76, 28);
        cancelBtn.SetBounds(260, 172, 76, 28);
        okBtn.Click += (_, _) =>
        {
            if (TaskTitle.Length == 0) { DialogResult = DialogResult.None; _title.Focus(); }
        };

        Controls.Add(new Label { Text = "任务", AutoSize = true, Location = new Point(16, 14) });
        Controls.Add(_title);
        Controls.Add(new Label { Text = "预估番茄数", AutoSize = true, Location = new Point(16, 72) });
        Controls.Add(_estimate);
        Controls.Add(_hasDue);
        Controls.Add(_due);
        Controls.Add(_done);
        Controls.Add(okBtn);
        Controls.Add(cancelBtn);

        AcceptButton = okBtn;
        CancelButton = cancelBtn;
    }
}
