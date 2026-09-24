using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace Medistock.Infrastructure.Tests;

public class ShortcutKeymapTests
{
    private class KeymapBinding
    {
        public string command { get; set; } = "";
        public string key { get; set; } = "";
        public string scope { get; set; } = "";
        public string description { get; set; } = "";
    }

    private class KeymapProfile
    {
        public string profileName { get; set; } = "";
        public string version { get; set; } = "";
        public string description { get; set; } = "";
        public KeymapBinding[] bindings { get; set; } = Array.Empty<KeymapBinding>();
    }

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Medistock.sln")))
        {
            dir = dir.Parent;
        }

        if (dir == null)
            throw new DirectoryNotFoundException("Could not find Medistock.sln root directory from " + AppContext.BaseDirectory);

        return dir.FullName;
    }

    [Fact]
    public void DefaultKeymap_LoadsSuccessfully_AndHasEssentialShortcuts()
    {
        var root = FindSolutionRoot();
        var keymapPath = Path.Combine(root, "src", "Clients", "Medistock.Desktop", "Keymaps", "default.json");

        Assert.True(File.Exists(keymapPath), $"Keymap file not found at: {keymapPath}");

        var json = File.ReadAllText(keymapPath);
        var profile = JsonSerializer.Deserialize<KeymapProfile>(json);

        Assert.NotNull(profile);
        Assert.Equal("Medistock Standard", profile.profileName);
        Assert.NotEmpty(profile.bindings);

        // Verify key bindings exist
        Assert.Contains(profile.bindings, b => b.command == "pos.save_invoice" && b.key == "Ctrl+S");
        Assert.Contains(profile.bindings, b => b.command == "app.help_shortcuts" && b.key == "Alt+F1");
        Assert.Contains(profile.bindings, b => b.command == "pos.payment" && b.key == "F6");
    }

    [Fact]
    public void MargKeymap_LoadsSuccessfully_AndHasMargSpecificShortcuts()
    {
        var root = FindSolutionRoot();
        var keymapPath = Path.Combine(root, "src", "Clients", "Medistock.Desktop", "Keymaps", "marg-compatible.json");

        Assert.True(File.Exists(keymapPath), $"Keymap file not found at: {keymapPath}");

        var json = File.ReadAllText(keymapPath);
        var profile = JsonSerializer.Deserialize<KeymapProfile>(json);

        Assert.NotNull(profile);
        Assert.Equal("MARG-Compatible", profile.profileName);
        Assert.NotEmpty(profile.bindings);

        // Verify MARG specifics: Ctrl+W is save, Ctrl+S is flush
        Assert.Contains(profile.bindings, b => b.command == "pos.save_invoice" && b.key == "Ctrl+W");
        Assert.Contains(profile.bindings, b => b.command == "app.flush_cache" && b.key == "Ctrl+S");
        Assert.Contains(profile.bindings, b => b.command == "app.help_shortcuts" && b.key == "Alt+F1");
    }
}
