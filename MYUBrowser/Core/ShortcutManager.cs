namespace MYUBrowser.Core;

/// <summary>一条可自定义的快捷键命令定义。</summary>
/// <param name="Id">稳定标识（持久化用）。</param>
/// <param name="Name">显示名。</param>
/// <param name="DefaultGesture">默认键位。</param>
/// <param name="IsGlobal">是否为系统级全局热键（RegisterHotKey，如老板键）。</param>
/// <param name="InPage">是否需要注入网页内生效（如透明度/极简）。</param>
public sealed record ShortcutCommand(string Id, string Name, Keys DefaultGesture, bool IsGlobal, bool InPage);

/// <summary>
/// 统一快捷键注册表：集中定义所有命令、当前绑定、持久化、按键匹配与展示。
/// 新增可自定义快捷键只需在 Commands 中加一条。
/// </summary>
public sealed class ShortcutManager
{
    private readonly AppSettings _settings;
    private readonly Dictionary<string, Keys> _bindings = new();

    public IReadOnlyList<ShortcutCommand> Commands { get; } = new List<ShortcutCommand>
    {
        new("boss.toggle",    "老板键（隐藏 / 唤醒）", Keys.Alt | Keys.Q,      true,  false),
        new("opacity.up",     "提高整体透明度",        Keys.Control | Keys.Up,   false, true),
        new("opacity.down",   "降低整体透明度",        Keys.Control | Keys.Down, false, true),
        new("minimal.toggle", "切换极简模式",          Keys.Control | Keys.M,    false, true),
        new("address.focus",  "聚焦地址栏（浏览器）",  Keys.Control | Keys.L,    false, false),
    };

    public ShortcutManager(AppSettings settings)
    {
        _settings = settings;
        Load();
    }

    private void Load()
    {
        foreach (var c in Commands) _bindings[c.Id] = c.DefaultGesture;

        foreach (var kv in _settings.ShortcutBindings)
            if (_bindings.ContainsKey(kv.Key))
                _bindings[kv.Key] = (Keys)kv.Value;

        // 老板键旧字段迁移（仅当新表未覆盖时）
        if (!_settings.ShortcutBindings.ContainsKey("boss.toggle") && _settings.BossKeyVirtualKey != 0)
            _bindings["boss.toggle"] = LegacyToKeys(_settings.BossKeyModifiers, _settings.BossKeyVirtualKey);
    }

    public Keys Get(string id) => _bindings.TryGetValue(id, out var k) ? k : Keys.None;
    public void Set(string id, Keys gesture) => _bindings[id] = gesture;

    public void ResetDefaults()
    {
        foreach (var c in Commands) _bindings[c.Id] = c.DefaultGesture;
    }

    /// <summary>某键位是否已被除 exceptId 之外的命令占用（冲突检测）。</summary>
    public string? FindConflict(Keys gesture, string exceptId)
    {
        foreach (var c in Commands)
            if (c.Id != exceptId && _bindings[c.Id] == gesture)
                return c.Name;
        return null;
    }

    /// <summary>匹配一个应用内（非全局）命令。</summary>
    public string? MatchLocal(Keys keyData)
    {
        foreach (var c in Commands)
            if (!c.IsGlobal && _bindings[c.Id] == keyData)
                return c.Id;
        return null;
    }

    public uint BossModifiers => KeysToModifiers(_bindings["boss.toggle"]);
    public uint BossVirtualKey => (uint)(_bindings["boss.toggle"] & Keys.KeyCode);

    /// <summary>需要注入网页的命令绑定。</summary>
    public IEnumerable<(string Id, Keys Gesture)> InPageBindings =>
        Commands.Where(c => c.InPage).Select(c => (c.Id, _bindings[c.Id]));

    public void Save()
    {
        _settings.ShortcutBindings = _bindings.ToDictionary(k => k.Key, v => (int)v.Value);
        var boss = _bindings["boss.toggle"];
        _settings.BossKeyModifiers = KeysToModifiers(boss);
        _settings.BossKeyVirtualKey = (uint)(boss & Keys.KeyCode);
        _settings.Save();
    }

    public static string Describe(Keys g)
    {
        var parts = new List<string>(4);
        if ((g & Keys.Control) != 0) parts.Add("Ctrl");
        if ((g & Keys.Alt) != 0) parts.Add("Alt");
        if ((g & Keys.Shift) != 0) parts.Add("Shift");
        parts.Add(DescribeKey(g & Keys.KeyCode));
        return string.Join(" + ", parts);
    }

    private static string DescribeKey(Keys key) => key switch
    {
        Keys.Up => "↑",
        Keys.Down => "↓",
        Keys.Left => "←",
        Keys.Right => "→",
        _ => key.ToString(),
    };

    private static uint KeysToModifiers(Keys g)
    {
        uint m = 0;
        if ((g & Keys.Control) != 0) m |= HotkeyManager.MOD_CONTROL;
        if ((g & Keys.Alt) != 0) m |= HotkeyManager.MOD_ALT;
        if ((g & Keys.Shift) != 0) m |= HotkeyManager.MOD_SHIFT;
        return m;
    }

    private static Keys LegacyToKeys(uint mods, uint vk)
    {
        Keys k = (Keys)vk;
        if ((mods & HotkeyManager.MOD_CONTROL) != 0) k |= Keys.Control;
        if ((mods & HotkeyManager.MOD_ALT) != 0) k |= Keys.Alt;
        if ((mods & HotkeyManager.MOD_SHIFT) != 0) k |= Keys.Shift;
        return k;
    }
}
