using System.Text.Json;

namespace MYUBrowser.Core;

public sealed record Bookmark(string Title, string Url);

/// <summary>书签存储，JSON 持久化到 %AppData%\MYUBrowser\bookmarks.json</summary>
public sealed class BookmarkStore
{
    private static string FilePath => Path.Combine(AppSettings.DataDir, "bookmarks.json");

    public List<Bookmark> Items { get; private set; } = new();

    public static BookmarkStore Load()
    {
        var store = new BookmarkStore();
        try
        {
            if (File.Exists(FilePath))
                store.Items = JsonSerializer.Deserialize<List<Bookmark>>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { }
        return store;
    }

    public bool Contains(string url) =>
        Items.Any(b => string.Equals(b.Url, url, StringComparison.OrdinalIgnoreCase));

    public void Add(string title, string url)
    {
        if (Contains(url)) return;
        Items.Add(new Bookmark(string.IsNullOrWhiteSpace(title) ? url : title, url));
        Save();
    }

    public void Remove(string url)
    {
        Items.RemoveAll(b => string.Equals(b.Url, url, StringComparison.OrdinalIgnoreCase));
        Save();
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(AppSettings.DataDir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Items, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
