using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FocusPomodoro.Models;
using FocusPomodoro.Services;

namespace FocusPomodoro;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly Action<AppSettings> _onSettingsSaved;

    public SettingsWindow(SettingsService settingsService, Action<AppSettings> onSettingsSaved, double currentOpacity = 0.75)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _onSettingsSaved = onSettingsSaved;
        ApplyOpacity(currentOpacity);
        LoadSettings();
    }

    private void ApplyOpacity(double opacity)
    {
        var alpha = (byte)(opacity * 255);
        MainBorder.Background = new SolidColorBrush(Color.FromArgb(alpha, 26, 26, 46));
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Settings;
        FocusMinutesTextBox.Text = settings.FocusMinutes.ToString();
        BreakMinutesTextBox.Text = settings.BreakMinutes.ToString();
        OpacitySlider.Value = settings.Opacity;
        UpdateOpacityLabel(settings.Opacity);
    }

    private void UpdateOpacityLabel(double opacity)
    {
        var percentage = (int)(opacity * 100);
        OpacityLabel.Text = $"Opacidad: {percentage}%";
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateOpacityLabel(e.NewValue);
    }

    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(FocusMinutesTextBox.Text, out var focusMinutes) || focusMinutes < 1 || focusMinutes > 120)
        {
            MessageBox.Show("Ingresa un tiempo de concentracion valido (1-120 minutos)", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(BreakMinutesTextBox.Text, out var breakMinutes) || breakMinutes < 1 || breakMinutes > 60)
        {
            MessageBox.Show("Ingresa un tiempo de descanso valido (1-60 minutos)", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var settings = new AppSettings
        {
            FocusMinutes = focusMinutes,
            BreakMinutes = breakMinutes,
            Opacity = OpacitySlider.Value
        };

        _settingsService.SaveSettings(settings);
        _onSettingsSaved(settings);
        Close();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            DragMove();
        }
        catch
        {
        }
    }
}
