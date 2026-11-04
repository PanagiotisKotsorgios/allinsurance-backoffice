using AllInsurance.Desktop.Views;
using Windows.System;

namespace AllInsurance.Desktop.Settings;

public sealed record ShortcutSpec(string Id, string Title, ShortcutBinding Default);

public sealed record ShortcutBinding(VirtualKey Key, VirtualKeyModifiers Modifiers)
{
    public string Display =>
        (Modifiers.HasFlag(VirtualKeyModifiers.Control) ? "Ctrl+" : "") +
        (Modifiers.HasFlag(VirtualKeyModifiers.Shift) ? "Shift+" : "") +
        (Modifiers.HasFlag(VirtualKeyModifiers.Menu) ? "Alt+" : "") +
        (Key is >= VirtualKey.Number0 and <= VirtualKey.Number9
            ? ((int)Key - (int)VirtualKey.Number0).ToString() : Key.ToString());
}

public static class ShortcutCatalog
{
    public static IReadOnlyList<ShortcutSpec> All { get; } =
    [
        .. WorkspaceSections.All.Select((section, index) => new ShortcutSpec(
            "section:" + section.Key, section.Title,
            section.Key == "renewals"
                ? new ShortcutBinding(VirtualKey.R,
                    VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift)
                : new ShortcutBinding((index > 6 ? index - 1 : index) == 9
                    ? VirtualKey.Number0
                    : (VirtualKey)((int)VirtualKey.Number1 + (index > 6 ? index - 1 : index)),
                    VirtualKeyModifiers.Control))),
        new("users", "Διαχείριση χρηστών", new(VirtualKey.U, VirtualKeyModifiers.Control)),
        new("audit", "Αρχείο ελέγχου", new(VirtualKey.J, VirtualKeyModifiers.Control)),
        new("backups", "Αντίγραφα ασφαλείας", new(VirtualKey.B, VirtualKeyModifiers.Control)),
        new("errors", "Ημερολόγιο σφαλμάτων", new(VirtualKey.L, VirtualKeyModifiers.Control)),
        new("shortcuts", "Διαχείριση συντομεύσεων", new(VirtualKey.K, VirtualKeyModifiers.Control)),
        new("settings", "Ρυθμίσεις συστήματος", new(VirtualKey.S, VirtualKeyModifiers.Control)),
        new("guide", "Οδηγός χρήσης", new(VirtualKey.F1, (VirtualKeyModifiers)0)),
        new("signout", "Αποσύνδεση", new(VirtualKey.Q,
            VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift))
    ];
}
