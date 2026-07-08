using System.Text.Json;

namespace MYUBrowser.Core;

public sealed class TodoTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public bool Done { get; set; }
    public int EstimatePomodoros { get; set; } = 1;
    public int CompletedPomodoros { get; set; }
    public DateTime? DueDate { get; set; }
}

/// <summary>待办清单存储，JSON 持久化到 %AppData%\MYUBrowser\todos.json</summary>
public sealed class TodoStore
{
    private static string FilePath => Path.Combine(AppSettings.DataDir, "todos.json");

    public List<TodoTask> Items { get; private set; } = new();

    public static TodoStore Load()
    {
        var store = new TodoStore();
        try
        {
            if (File.Exists(FilePath))
                store.Items = JsonSerializer.Deserialize<List<TodoTask>>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { }
        return store;
    }

    public TodoTask Add(string title, int estimate, DateTime? due = null)
    {
        var task = new TodoTask
        {
            Title = title.Trim(),
            EstimatePomodoros = Math.Max(1, estimate),
            DueDate = due,
        };
        Items.Add(task);
        Save();
        return task;
    }

    public void Edit(string id, string title, int estimate, DateTime? due, bool done)
    {
        var t = Items.FirstOrDefault(x => x.Id == id);
        if (t == null) return;
        t.Title = title.Trim();
        t.EstimatePomodoros = Math.Max(1, estimate);
        t.DueDate = due;
        t.Done = done;
        Save();
    }

    public void Remove(string id)
    {
        Items.RemoveAll(t => t.Id == id);
        Save();
    }

    public void Toggle(string id)
    {
        var t = Items.FirstOrDefault(x => x.Id == id);
        if (t == null) return;
        t.Done = !t.Done;
        Save();
    }

    public void AddPomodoro(string id)
    {
        var t = Items.FirstOrDefault(x => x.Id == id);
        if (t == null) return;
        t.CompletedPomodoros++;
        Save();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppSettings.DataDir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Items, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
