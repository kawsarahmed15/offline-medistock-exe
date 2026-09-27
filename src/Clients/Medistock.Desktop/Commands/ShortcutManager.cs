using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Windows.System;

namespace Medistock.Desktop.Commands;

public class KeyBindingDefinition
{
    public string Command { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Scope { get; set; } = "Global";
    public string Description { get; set; } = string.Empty;
}

public class KeymapProfile
{
    public string ProfileName { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0";
    public string Description { get; set; } = string.Empty;
    public List<KeyBindingDefinition> Bindings { get; set; } = new();
}

public interface IShortcutService
{
    KeymapProfile CurrentProfile { get; }
    void LoadProfile(string profileName);
    void RegisterAction(string commandName, Action action);
    bool TryExecuteShortcut(VirtualKey key, bool isCtrl, bool isAlt, bool isShift, string activeScope);
    string GetShortcutText(string commandName);
    IReadOnlyList<KeyBindingDefinition> GetActiveShortcuts(string activeScope);
}

public class ShortcutService : IShortcutService
{
    private readonly Dictionary<string, Action> _registeredActions = new(StringComparer.OrdinalIgnoreCase);
    private KeymapProfile _currentProfile = new();

    public KeymapProfile CurrentProfile => _currentProfile;

    public ShortcutService()
    {
        LoadDefaultProfile();
    }

    public void LoadProfile(string profileName)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var fileName = profileName.Equals("MARG-Compatible", StringComparison.OrdinalIgnoreCase)
            ? "marg-compatible.json"
            : "default.json";

        var path = Path.Combine(baseDir, "Keymaps", fileName);
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            var profile = JsonSerializer.Deserialize<KeymapProfile>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (profile != null)
            {
                _currentProfile = profile;
                return;
            }
        }

        LoadDefaultProfile();
    }

    public void RegisterAction(string commandName, Action action)
    {
        if (string.IsNullOrWhiteSpace(commandName)) return;
        _registeredActions[commandName] = action;
    }

    public bool TryExecuteShortcut(VirtualKey key, bool isCtrl, bool isAlt, bool isShift, string activeScope)
    {
        var chord = BuildKeyChord(key, isCtrl, isAlt, isShift);
        if (string.IsNullOrEmpty(chord)) return false;

        // Scope resolution: Exact active scope first, fallback to Global
        var binding = _currentProfile.Bindings
            .Where(b => string.Equals(b.Key, chord, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(b => string.Equals(b.Scope, activeScope, StringComparison.OrdinalIgnoreCase) ? 2 : (string.Equals(b.Scope, "Global", StringComparison.OrdinalIgnoreCase) ? 1 : 0))
            .FirstOrDefault();

        if (binding == null) return false;

        if (_registeredActions.TryGetValue(binding.Command, out var action))
        {
            action.Invoke();
            return true;
        }

        return false;
    }

    public string GetShortcutText(string commandName)
    {
        var binding = _currentProfile.Bindings.FirstOrDefault(b => string.Equals(b.Command, commandName, StringComparison.OrdinalIgnoreCase));
        return binding?.Key ?? string.Empty;
    }

    public IReadOnlyList<KeyBindingDefinition> GetActiveShortcuts(string activeScope)
    {
        return _currentProfile.Bindings
            .Where(b => string.Equals(b.Scope, "Global", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(b.Scope, activeScope, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static string BuildKeyChord(VirtualKey key, bool isCtrl, bool isAlt, bool isShift)
    {
        var parts = new List<string>();
        if (isCtrl) parts.Add("Ctrl");
        if (isAlt) parts.Add("Alt");
        if (isShift) parts.Add("Shift");

        var keyName = key switch
        {
            VirtualKey.F1 => "F1",
            VirtualKey.F2 => "F2",
            VirtualKey.F3 => "F3",
            VirtualKey.F4 => "F4",
            VirtualKey.F5 => "F5",
            VirtualKey.F6 => "F6",
            VirtualKey.F7 => "F7",
            VirtualKey.F8 => "F8",
            VirtualKey.F9 => "F9",
            VirtualKey.F10 => "F10",
            VirtualKey.F11 => "F11",
            VirtualKey.F12 => "F12",
            VirtualKey.Escape => "Escape",
            VirtualKey.Enter => "Enter",
            VirtualKey.Tab => "Tab",
            VirtualKey.Number1 => "1",
            VirtualKey.Number2 => "2",
            VirtualKey.Number3 => "3",
            VirtualKey.Number4 => "4",
            VirtualKey.Number5 => "5",
            VirtualKey.A => "A",
            VirtualKey.H => "H",
            VirtualKey.P => "P",
            VirtualKey.R => "R",
            VirtualKey.S => "S",
            VirtualKey.T => "T",
            VirtualKey.W => "W",
            _ => key.ToString()
        };

        parts.Add(keyName);
        return string.Join("+", parts);
    }

    private void LoadDefaultProfile()
    {
        _currentProfile = new KeymapProfile
        {
            ProfileName = "Medistock Standard",
            Version = "1.0",
            Description = "Default Medistock Standard Keymap",
            Bindings = new List<KeyBindingDefinition>
            {
                new() { Command = "app.help_shortcuts", Key = "F1", Scope = "Global", Description = "Open keyboard shortcut help overlay" },
                new() { Command = "app.help_shortcuts", Key = "Alt+F1", Scope = "Global", Description = "Open keyboard shortcut help overlay" },
                new() { Command = "pos.new_tab", Key = "Ctrl+T", Scope = "POS", Description = "Open a new invoice tab" },
                new() { Command = "pos.close_tab", Key = "Ctrl+W", Scope = "POS", Description = "Close current invoice tab" },
                new() { Command = "pos.next_tab", Key = "Ctrl+Tab", Scope = "POS", Description = "Switch to next invoice tab" },
                new() { Command = "pos.prev_tab", Key = "Ctrl+Shift+Tab", Scope = "POS", Description = "Switch to previous invoice tab" },
                new() { Command = "pos.new_sale", Key = "F2", Scope = "POS", Description = "Start a new / fresh sale" },
                new() { Command = "pos.search_product", Key = "F3", Scope = "POS", Description = "Focus product search / scan box" },
                new() { Command = "pos.apply_discount", Key = "F4", Scope = "POS", Description = "Open line / bill discount panel" },
                new() { Command = "pos.payment", Key = "F6", Scope = "POS", Description = "Open payment & finalize sale" },
                new() { Command = "pos.hold_bill", Key = "Ctrl+H", Scope = "POS", Description = "Hold current bill in queue" },
                new() { Command = "pos.resume_bill", Key = "Ctrl+R", Scope = "POS", Description = "Resume held bill" },
                new() { Command = "pos.save_invoice", Key = "Ctrl+S", Scope = "POS", Description = "Save current invoice" },
                new() { Command = "pos.print", Key = "Ctrl+P", Scope = "POS", Description = "Print receipt / open print prompt" },
                new() { Command = "pos.print", Key = "F7", Scope = "POS", Description = "Print receipt / open print prompt" },
                new() { Command = "pos.clear_cart", Key = "Escape", Scope = "POS", Description = "Clear search or active selection" }
            }
        };
    }
}

