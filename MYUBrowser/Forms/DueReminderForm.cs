namespace MYUBrowser.Forms;

/// <summary>待办临近/逾期提醒弹窗（TopMost，不打断老板键隐藏策略——由调用方决定何时弹出）。</summary>
public sealed class DueReminderForm : Form
{
    public DueReminderForm(string title, string message)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        BackColor = Color.FromArgb(37, 37, 38);
        ForeColor = Color.Gainsboro;
        Font = new Font("Segoe UI", 9f);
        ClientSize = new Size(360, 130);

        var msg = new Label
        {
            Text = message,
            AutoSize = false,
            Bounds = new Rectangle(16, 16, 328, 56),
            ForeColor = Color.Gainsboro,
        };

        var ok = new Button
        {
            Text = "知道了",
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat,
            Bounds = new Rectangle(268, 88, 76, 28),
        };
        ok.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);

        Controls.Add(msg);
        Controls.Add(ok);
        AcceptButton = ok;
    }
}
