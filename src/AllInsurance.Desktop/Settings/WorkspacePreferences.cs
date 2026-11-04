using Microsoft.UI.Xaml;
using System.IO;
using System.Text.Json;
using Windows.System;

namespace AllInsurance.Desktop.Settings;

public sealed class WorkspacePreferences
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AllInsurance",
        "ui-settings.json");

    public ElementTheme Theme { get; private set; } = ElementTheme.Default;
    public bool EyeComfortEnabled { get; private set; }
    public bool ShortcutsEnabled { get; private set; } = true;
    private Dictionary<string, ShortcutBinding?> _shortcuts = [];
    public event EventHandler? ShortcutsChanged;

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

            EyeComfortEnabled = saved.Theme == "EyeComfort";
            Theme = saved.Theme switch
            {
                "Light" => ElementTheme.Light,
                "EyeComfort" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
            ShortcutsEnabled = saved.ShortcutsEnabled;
            _shortcuts = saved.Shortcuts ?? [];
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
        EyeComfortEnabled = false;
        Save();
    }

    public void SetEyeComfort()
    {
        Theme = ElementTheme.Light;
        EyeComfortEnabled = true;
        Save();
    }

    public void SetShortcutsEnabled(bool enabled)
    {
        ShortcutsEnabled = enabled;
        Save();
        ShortcutsChanged?.Invoke(this, EventArgs.Empty);
    }

    public ShortcutBinding? GetShortcut(string actionId)
    {
        if (_shortcuts.TryGetValue(actionId, out var binding)) return binding;
        return ShortcutCatalog.All.FirstOrDefault(spec => spec.Id == actionId)?.Default;
    }

    public void SetShortcut(string actionId, ShortcutBinding? binding)
    {
        if (!ShortcutCatalog.All.Any(spec => spec.Id == actionId))
            throw new ArgumentException("Άγνωστη ενέργεια συντόμευσης.");
        if (binding is not null)
        {
            if (binding.Modifiers == 0 && binding.Key is < VirtualKey.F1 or > VirtualKey.F12)
                throw new ArgumentException("Χρησιμοποιήστε Ctrl, Shift, Alt ή πλήκτρο F1–F12.");
            var conflict = ShortcutCatalog.All.FirstOrDefault(spec => spec.Id != actionId &&
                GetShortcut(spec.Id) == binding);
            if (conflict is not null)
                throw new InvalidOperationException($"Ο συνδυασμός χρησιμοποιείται ήδη από: {conflict.Title}.");
        }
        _shortcuts[actionId] = binding;
        Save();
        ShortcutsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(new SavedPreferences(
            EyeComfortEnabled ? "EyeComfort" : Theme.ToString(), ShortcutsEnabled, _shortcuts)));
    }

    private sealed record SavedPreferences(string Theme, bool ShortcutsEnabled,
        Dictionary<string, ShortcutBinding?>? Shortcuts = null);
}
