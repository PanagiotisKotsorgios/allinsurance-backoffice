using Microsoft.UI.Xaml;
using System.IO;
using System.Text.Json;

namespace AllInsurance.Desktop.Settings;

public sealed class WorkspacePreferences
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AllInsurance",
        "ui-settings.json");

    public ElementTheme Theme { get; private set; } = ElementTheme.Default;
    public bool ShortcutsEnabled { get; private set; } = true;

    public WorkspacePreferences()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return;
            }

            var saved = JsonSerializer.Deserialize<SavedPreferences>(File.ReadAllText(_path));
            if (saved is null)
            {
                return;
            }

            Theme = saved.Theme switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
            ShortcutsEnabled = saved.ShortcutsEnabled;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Theme = ElementTheme.Default;
            ShortcutsEnabled = true;
        }
    }

    public void SetTheme(ElementTheme theme)
    {
        Theme = theme;
        Save();
    }

    public void SetShortcutsEnabled(bool enabled)
    {
        ShortcutsEnabled = enabled;
        Save();
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(new SavedPreferences(Theme.ToString(), ShortcutsEnabled)));
    }

    private sealed record SavedPreferences(string Theme, bool ShortcutsEnabled);
}
