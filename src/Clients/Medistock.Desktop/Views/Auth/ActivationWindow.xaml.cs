using System;
using System.Threading.Tasks;
using Medistock.Infrastructure.Identity.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;

namespace Medistock.Desktop.Views.Auth;

/// <summary>
/// ViewModel for the activation screen (lightweight, no MVVM framework needed here).
/// </summary>
public class ActivationViewModel
{
    private string _statusMessage = string.Empty;
    private bool _isBusy;

    public string ButtonText => _isBusy ? "Activating..." : "Activate Software";
    public bool IsNotBusy => !_isBusy;
    public bool IsProgressVisible => _isBusy ? true : false;
    public string StatusMessage => _statusMessage;

    // Red for errors, green for success
    public SolidColorBrush StatusColor { get; private set; } =
        new SolidColorBrush(Colors.Transparent);

    public void SetBusy(bool busy)
    {
        _isBusy = busy;
        if (busy)
        {
            _statusMessage = "Connecting to Medistock servers...";
            StatusColor = new SolidColorBrush(Colors.Gray);
        }
    }

    public void SetError(string message)
    {
        _isBusy = false;
        _statusMessage = message;
        StatusColor = new SolidColorBrush(Colors.OrangeRed);
    }

    public void SetSuccess(string message)
    {
        _isBusy = false;
        _statusMessage = message;
        StatusColor = new SolidColorBrush(Colors.LightGreen);
    }
}

/// <summary>
/// Shown on first launch or when license is invalid.
/// Fires ActivationSucceeded event to allow App.xaml.cs to open MainWindow.
/// </summary>
public sealed partial class ActivationWindow : Window
{
    private readonly IActivationService _activationService;

    public ActivationViewModel ViewModel { get; } = new();

    /// <summary>Raised when the user successfully activates the software.</summary>
    public event Action? ActivationSucceeded;

    public ActivationWindow(IActivationService activationService)
    {
        _activationService = activationService ?? throw new ArgumentNullException(nameof(activationService));
        InitializeComponent();

        // Resize and center the window
        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new Windows.Graphics.SizeInt32(520, 560));

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsMaximizable = false;
                presenter.IsResizable = false;
            }

            // Center on screen
            var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
            var x = (displayArea.WorkArea.Width - 520) / 2;
            var y = (displayArea.WorkArea.Height - 560) / 2;
            appWindow.Move(new Windows.Graphics.PointInt32(x, y));
        }
        catch { }
    }

    private void ActivateButton_Click(object sender, RoutedEventArgs e)
        => _ = DoActivateAsync();

    private void InputBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
            _ = DoActivateAsync();
    }

    private async Task DoActivateAsync()
    {
        var email = EmailBox.Text?.Trim();
        var password = PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(email))
        {
            ViewModel.SetError("Please enter your email address.");
            Bindings.Update();
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            ViewModel.SetError("Please enter your password.");
            Bindings.Update();
            return;
        }

        ViewModel.SetBusy(true);
        Bindings.Update();

        var result = await _activationService.ActivateAsync(email, password);

        if (result.Success)
        {
            ViewModel.SetSuccess("✓ Activation successful! Opening Medistock...");
            Bindings.Update();

            await Task.Delay(800); // Brief moment for user to see success state
            ActivationSucceeded?.Invoke();
        }
        else
        {
            ViewModel.SetError(result.ErrorMessage ?? "Activation failed. Please try again.");
            Bindings.Update();
        }
    }
}
