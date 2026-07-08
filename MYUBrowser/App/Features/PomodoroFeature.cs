using MYUBrowser.Core;

namespace MYUBrowser.App.Features;

/// <summary>
/// 番茄钟功能：把 PomodoroTimer 挂到 AppHost 的每秒事件上驱动，负责阶段切换的托盘提醒、
/// 专注期可选降透明、以及与待办清单的联动（完成一个专注番茄给当前任务 +1）。
/// UI（PomodoroView）只观察本功能、发送命令，不含任何计时逻辑。
/// </summary>
public sealed class PomodoroFeature : IAppFeature
{
    private readonly AppSettings _settings;
    private AppHost? _host;
    private bool _focusDimApplied;

    public PomodoroTimer Timer { get; } = new();
    public TodoStore Todos { get; }

    /// <summary>当前绑定的任务 Id（完成专注番茄时 +1）。</summary>
    public string? ActiveTaskId { get; set; }

    /// <summary>计时状态变化（供视图/指示器刷新计时显示）。</summary>
    public event Action? TimerChanged;

    /// <summary>待办数据变化（增删改/记番茄），供视图重建清单。</summary>
    public event Action? TodosChanged;

    public PomodoroFeature(AppSettings settings, TodoStore todos)
    {
        _settings = settings;
        Todos = todos;
        ApplyConfig();
    }

    public void Attach(AppHost host)
    {
        _host = host;
        ApplyConfig();
        host.SecondTick += OnSecond;
        host.Services.SettingsApplied += OnSettingsApplied;
        Timer.Changed += OnTimerChanged;
        Timer.PhaseCompleted += OnPhaseCompleted;
    }

    // ---- 供视图调用的命令 ----
    public void Start() => Timer.Start();
    public void Pause() => Timer.Pause();
    public void TogglePlay() { if (Timer.Running) Timer.Pause(); else Timer.Start(); }
    public void Reset() => Timer.Reset();
    public void Skip() => Timer.Skip();

    public TodoTask AddTask(string title, int estimate, DateTime? due = null)
    {
        var t = Todos.Add(title, estimate, due);
        TodosChanged?.Invoke();
        return t;
    }

    public void EditTask(string id, string title, int estimate, DateTime? due, bool done)
    {
        Todos.Edit(id, title, estimate, due, done);
        TodosChanged?.Invoke();
    }

    public void RemoveTask(string id)
    {
        Todos.Remove(id);
        if (ActiveTaskId == id) ActiveTaskId = null;
        TodosChanged?.Invoke();
    }

    public void ToggleTask(string id)
    {
        Todos.Toggle(id);
        TodosChanged?.Invoke();
    }

    public void SetActiveTask(string? id)
    {
        ActiveTaskId = id;
        TodosChanged?.Invoke();
    }

    // ---- 内部 ----
    private void OnSecond() => Timer.Tick();

    private void OnTimerChanged()
    {
        UpdateFocusDim();
        TimerChanged?.Invoke();
    }

    private void OnSettingsApplied()
    {
        ApplyConfig();
        Timer.SyncConfig();
    }

    private void ApplyConfig()
    {
        Timer.FocusMinutes = Math.Max(1, _settings.PomodoroMinutes);
        Timer.BreakMinutes = Math.Max(1, _settings.PomodoroBreakMinutes);
        Timer.LongBreakMinutes = Math.Max(1, _settings.PomodoroLongBreakMinutes);
        Timer.PomodorosPerLongBreak = Math.Max(1, _settings.PomodorosPerLongBreak);
        Timer.AutoStartNext = _settings.PomodoroAutoStartNext;
    }

    private void OnPhaseCompleted(PomodoroPhase finished)
    {
        if (finished == PomodoroPhase.Focus)
        {
            if (ActiveTaskId != null)
            {
                Todos.AddPomodoro(ActiveTaskId);
                TodosChanged?.Invoke();
            }
            Notify("专注完成 🍅", "干得漂亮，休息一下 ☕");
        }
        else
        {
            Notify("休息结束 ☕", "回到专注，继续推进任务 🍅");
        }
    }

    private void Notify(string title, string text)
    {
        if (_settings.PomodoroNotify)
            _host?.Services.ShowBalloon(4000, title, text, ToolTipIcon.Info);
    }

    /// <summary>专注期把整窗透明度降到目标值，离开专注时恢复到用户设置值。事件驱动，不逐秒设置。</summary>
    private void UpdateFocusDim()
    {
        if (_host == null || !_settings.PomodoroDimOnFocus) return;

        bool shouldDim = Timer.Phase == PomodoroPhase.Focus && Timer.Running;
        if (shouldDim && !_focusDimApplied)
        {
            _host.SetOpacity(_settings.PomodoroFocusDimOpacity, persist: false);
            _focusDimApplied = true;
        }
        else if (!shouldDim && _focusDimApplied)
        {
            _host.SetOpacity(_host.UserOpacity, persist: false);
            _focusDimApplied = false;
        }
    }

    public void Dispose()
    {
        if (_host != null)
        {
            _host.SecondTick -= OnSecond;
            _host.Services.SettingsApplied -= OnSettingsApplied;
        }
        Timer.Changed -= OnTimerChanged;
        Timer.PhaseCompleted -= OnPhaseCompleted;
    }
}
