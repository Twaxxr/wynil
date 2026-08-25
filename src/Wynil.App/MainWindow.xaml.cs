using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Wynil.App.ViewModels;

namespace Wynil.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private bool _trackSettingsChanges;

    public MainWindow(MainWindowViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        SourceInitialized += OnSourceInitialized;
        ContentRendered += OnContentRendered;
        StateChanged += OnStateChanged;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var rounded = 2;
        var dark = 1;
        _ = DwmSetWindowAttribute(handle, 33, ref rounded, sizeof(int));
        _ = DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
    }

    private void OnContentRendered(object? sender, EventArgs e)
    {
        if (_trackSettingsChanges) return;
        SettingsHost.AddHandler(ToggleButton.ClickEvent, new RoutedEventHandler(OnSettingChanged));
        SettingsHost.AddHandler(System.Windows.Controls.Primitives.Selector.SelectionChangedEvent, new RoutedEventHandler(OnSettingChanged));
        SettingsHost.AddHandler(System.Windows.Controls.TextBox.TextChangedEvent, new RoutedEventHandler(OnSettingChanged));
        SettingsHost.AddHandler(RangeBase.ValueChangedEvent, new RoutedEventHandler(OnSettingChanged));
        _ = Dispatcher.BeginInvoke(() => _trackSettingsChanges = true, DispatcherPriority.ContextIdle);
    }

    private async void OnSettingChanged(object sender, RoutedEventArgs e)
    {
        if (_trackSettingsChanges) await _viewModel.MarkSettingsDirtyAsync();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) ToggleMaximize();
        else if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnStateChanged(object? sender, EventArgs e)
    {
        MaximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
        WindowFrame.CornerRadius = WindowState == WindowState.Maximized ? new CornerRadius(0) : new CornerRadius(11);
    }

    protected override void OnClosed(EventArgs e)
    {
        NowPlayingCard.Dispose();
        SourceInitialized -= OnSourceInitialized;
        ContentRendered -= OnContentRendered;
        StateChanged -= OnStateChanged;
        base.OnClosed(e);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int attributeValue, int attributeSize);
}
