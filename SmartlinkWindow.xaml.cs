using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;

namespace FocusPomodoro;

public partial class SmartlinkWindow : Window
{
    private readonly DispatcherTimer _countdownTimer;
    private int _secondsLeft = 5;
    private const int TotalSeconds = 5;
    private readonly Action _onClosed;

    public SmartlinkWindow(Action onClosed)
    {
        InitializeComponent();
        _onClosed = onClosed;

        _countdownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _countdownTimer.Tick += CountdownTick;

        Loaded += SmartlinkWindow_Loaded;
        Closed += (_, _) =>
        {
            _countdownTimer.Stop();
            SmartlinkWebView?.Dispose();
        };
    }

    private async void SmartlinkWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(
                userDataFolder: Path.Combine(Path.GetTempPath(), "FocusPomodoro_WebView2"));

            await SmartlinkWebView.EnsureCoreWebView2Async(env);

            SmartlinkWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            SmartlinkWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            SmartlinkWebView.CoreWebView2.Settings.IsZoomControlEnabled = false;

            SmartlinkWebView.CoreWebView2.NewWindowRequested += (s, args) =>
            {
                args.Handled = true;
                Process.Start(new ProcessStartInfo(args.Uri) { UseShellExecute = true });
            };

            SmartlinkWebView.CoreWebView2.Navigate("https://www.effectivegatecpm.com/nke0sa1w?key=070a0fac91f3c1d2651061945534a509");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Smartlink init failed: {ex.Message}");
        }

        // Animar progress bar de 0 a ancho completo en 5 segundos
        StartProgressAnimation();
        _countdownTimer.Start();
    }

    private void StartProgressAnimation()
    {
        // Obtener el ancho del contenedor padre
        var parentWidth = ActualWidth - 40; // 20 margin cada lado
        var anim = new DoubleAnimation
        {
            From = 0,
            To = parentWidth,
            Duration = TimeSpan.FromSeconds(TotalSeconds),
            EasingFunction = null
        };
        ProgressBar.BeginAnimation(WidthProperty, anim);
    }

    private void CountdownTick(object? sender, EventArgs e)
    {
        _secondsLeft--;
        CountdownText.Text = _secondsLeft.ToString();

        if (_secondsLeft <= 0)
        {
            _countdownTimer.Stop();
            _onClosed.Invoke();
            Close();
        }
    }
}
