namespace MYUBrowser.Core;

public enum PomodoroPhase { Focus, ShortBreak, LongBreak }

/// <summary>
/// 番茄钟纯状态机：管理阶段、剩余时间、循环计数，由外部每秒调用 Tick() 驱动。不含任何 UI 或线程。
/// </summary>
public sealed class PomodoroTimer
{
    // ---- 配置（由 Feature 从设置注入）----
    public int FocusMinutes { get; set; } = 25;
    public int BreakMinutes { get; set; } = 5;
    public int LongBreakMinutes { get; set; } = 15;
    public int PomodorosPerLongBreak { get; set; } = 4;
    public bool AutoStartNext { get; set; }

    // ---- 状态 ----
    public PomodoroPhase Phase { get; private set; } = PomodoroPhase.Focus;
    public int RemainingSeconds { get; private set; }
    public bool Running { get; private set; }
    public int CompletedFocus { get; private set; }

    /// <summary>任意状态变化（供 UI/指示器刷新）。</summary>
    public event Action? Changed;

    /// <summary>某阶段自然计时结束（Feature 据此记番茄/发提醒）。</summary>
    public event Action<PomodoroPhase>? PhaseCompleted;

    public PomodoroTimer() => RemainingSeconds = FocusMinutes * 60;

    public void Start()
    {
        Running = true;
        Changed?.Invoke();
    }

    public void Pause()
    {
        Running = false;
        Changed?.Invoke();
    }

    public void Reset()
    {
        Running = false;
        Phase = PomodoroPhase.Focus;
        CompletedFocus = 0;
        RemainingSeconds = FocusMinutes * 60;
        Changed?.Invoke();
    }

    /// <summary>手动跳到下一阶段（不计番茄、不发提醒）。</summary>
    public void Skip()
    {
        Phase = NextPhase(Phase);
        RemainingSeconds = MinutesFor(Phase) * 60;
        Running = false;
        Changed?.Invoke();
    }

    public void Tick()
    {
        if (!Running) return;
        RemainingSeconds--;
        if (RemainingSeconds > 0)
        {
            Changed?.Invoke();
            return;
        }
        CompleteCurrentPhase();
    }

    /// <summary>配置变化后同步：未运行时刷新当前阶段的时长显示。</summary>
    public void SyncConfig()
    {
        if (!Running) RemainingSeconds = MinutesFor(Phase) * 60;
        Changed?.Invoke();
    }

    private void CompleteCurrentPhase()
    {
        var finished = Phase;
        if (finished == PomodoroPhase.Focus) CompletedFocus++;

        PhaseCompleted?.Invoke(finished);

        Phase = NextPhase(finished);
        RemainingSeconds = MinutesFor(Phase) * 60;
        Running = AutoStartNext;
        Changed?.Invoke();
    }

    private PomodoroPhase NextPhase(PomodoroPhase finished)
    {
        if (finished == PomodoroPhase.Focus)
            return (CompletedFocus > 0 && CompletedFocus % Math.Max(1, PomodorosPerLongBreak) == 0)
                ? PomodoroPhase.LongBreak
                : PomodoroPhase.ShortBreak;
        return PomodoroPhase.Focus;
    }

    private int MinutesFor(PomodoroPhase p) => p switch
    {
        PomodoroPhase.Focus => Math.Max(1, FocusMinutes),
        PomodoroPhase.ShortBreak => Math.Max(1, BreakMinutes),
        PomodoroPhase.LongBreak => Math.Max(1, LongBreakMinutes),
        _ => 1,
    };
}
