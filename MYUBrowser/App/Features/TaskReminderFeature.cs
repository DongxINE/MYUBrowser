using MYUBrowser.Core;
using MYUBrowser.Forms;

namespace MYUBrowser.App.Features;

/// <summary>
/// 待办截止提醒：扫描共享待办清单，未完成且设置了截止日期的任务，在「距截止 ≤ 提前量」时弹窗提醒（每个任务仅一次）。
/// 老板键隐藏期间不弹窗，唤回后再触发。
/// </summary>
public sealed class TaskReminderFeature : IAppFeature
{
    private readonly AppSettings _settings;
    private readonly TodoStore _todos;
    private readonly HashSet<string> _reminded = new();
    private AppHost? _host;

    public TaskReminderFeature(AppSettings settings, TodoStore todos)
    {
        _settings = settings;
        _todos = todos;
    }

    public void Attach(AppHost host)
    {
        _host = host;
        host.SecondTick += OnSecond;
    }

    private void OnSecond()
    {
        if (_host == null || !_settings.TodoRemindEnabled || _host.IsHidden) return;

        var now = DateTime.Now;
        var lead = TimeSpan.FromMinutes(Math.Max(1, _settings.TodoReminderLeadMinutes));

        foreach (var t in _todos.Items)
        {
            if (t.Done || t.DueDate == null)
            {
                _reminded.Remove(t.Id);
                continue;
            }

            var due = t.DueDate.Value;
            if (now < due - lead)
            {
                _reminded.Remove(t.Id);
                continue;
            }

            if (_reminded.Contains(t.Id)) continue;

            _reminded.Add(t.Id);
            using var dlg = new DueReminderForm("待办提醒", BuildText(t, now, due));
            _host.ShowModal(dlg);
            break; // 一次只弹一个，避免连续阻塞
        }
    }

    private static string BuildText(TodoTask t, DateTime now, DateTime due)
    {
        string when;
        if (due <= now)
        {
            var over = now - due;
            when = over.TotalMinutes < 60
                ? $"已逾期 {Math.Max(1, (int)over.TotalMinutes)} 分钟"
                : $"已逾期 {(int)over.TotalHours} 小时";
        }
        else
        {
            var left = due - now;
            when = left.TotalMinutes < 60
                ? $"还有 {Math.Max(1, (int)left.TotalMinutes)} 分钟到期"
                : left.TotalHours < 24
                    ? $"还有 {(int)left.TotalHours} 小时到期"
                    : $"还有 {(int)left.TotalDays} 天到期";
        }
        return $"{t.Title}\n\n{when}（截止 {due:MM-dd HH:mm}）";
    }

    public void Dispose()
    {
        if (_host != null) _host.SecondTick -= OnSecond;
    }
}
