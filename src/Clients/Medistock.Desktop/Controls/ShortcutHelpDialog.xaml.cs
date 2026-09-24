using System.Collections.Generic;
using Medistock.Desktop.Commands;
using Microsoft.UI.Xaml.Controls;

namespace Medistock.Desktop.Controls;

public sealed partial class ShortcutHelpDialog : ContentDialog
{
    public ShortcutHelpDialog(string profileName, IReadOnlyList<KeyBindingDefinition> shortcuts)
    {
        this.InitializeComponent();

        ProfileHeader.Text = $"Active Keymap Profile: {profileName} (Scope: POS & Global)";
        ShortcutsList.ItemsSource = shortcuts;
    }
}
