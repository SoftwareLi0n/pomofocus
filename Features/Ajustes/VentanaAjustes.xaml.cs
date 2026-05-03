using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FocusPomodoro.Core.Models;
using FocusPomodoro.Core.Services;

namespace FocusPomodoro;

public partial class VentanaAjustes : Window
{
    private readonly ServicioAjustes _settingsService;
    private readonly Action<AjustesApp> _onSettingsSaved;

    public VentanaAjustes(ServicioAjustes settingsService, Action<AjustesApp> onSettingsSaved, double currentOpacity = 0.75)
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

        LoadWorkHoursComboBoxes();
        SelectWorkHours(settings);

        if (settings.IsDrastic)
        {
            DrasticRadio.IsChecked = true;
            LevelDescription.Text = "Sin pausa, sin reset, sin saltar descanso";
        }
        else
        {
            ModerateRadio.IsChecked = true;
            LevelDescription.Text = "Permite pausar, resetear y saltar descanso";
        }

        ModerateRadio.Checked += (s, e) => LevelDescription.Text = "Permite pausar, resetear y saltar descanso";
        DrasticRadio.Checked += (s, e) => LevelDescription.Text = "Sin pausa, sin reset, sin saltar descanso";
    }

    private void LoadWorkHoursComboBoxes()
    {
        var itemStyle = new Style(typeof(ComboBoxItem));
        itemStyle.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, new SolidColorBrush(Color.FromArgb(128, 45, 45, 68))));
        itemStyle.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, Brushes.White));
        itemStyle.Setters.Add(new Setter(ComboBoxItem.PaddingProperty, new Thickness(8, 4, 8, 4)));

        var highlightTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        highlightTrigger.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, new SolidColorBrush(Color.FromArgb(128, 77, 77, 102))));
        itemStyle.Triggers.Add(highlightTrigger);

        for (int hour = 0; hour < 24; hour++)
        {
            var time = new TimeSpan(hour, 0, 0);
            StartHourCombo.Items.Add(time.ToString(@"hh\:mm"));
            EndHourCombo.Items.Add(time.ToString(@"hh\:mm"));
        }
        for (int hour = 0; hour < 24; hour++)
        {
            var time = new TimeSpan(hour, 30, 0);
            StartHourCombo.Items.Add(time.ToString(@"hh\:mm"));
            EndHourCombo.Items.Add(time.ToString(@"hh\:mm"));
        }

        StartHourCombo.ItemContainerStyle = itemStyle;
        EndHourCombo.ItemContainerStyle = itemStyle;
    }

    private void SelectWorkHours(AjustesApp settings)
    {
        var startTimeStr = settings.WorkStartTime.ToString(@"hh\:mm");
        var endTimeStr = settings.WorkEndTime.ToString(@"hh\:mm");

        for (int i = 0; i < StartHourCombo.Items.Count; i++)
        {
            if (StartHourCombo.Items[i].ToString() == startTimeStr)
            {
                StartHourCombo.SelectedIndex = i;
                break;
            }
        }

        for (int i = 0; i < EndHourCombo.Items.Count; i++)
        {
            if (EndHourCombo.Items[i].ToString() == endTimeStr)
            {
                EndHourCombo.SelectedIndex = i;
                break;
            }
        }
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

        if (StartHourCombo.SelectedItem == null || EndHourCombo.SelectedItem == null)
        {
            MessageBox.Show("Selecciona el horario de trabajo", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var startParts = StartHourCombo.SelectedItem.ToString().Split(':');
        var endParts = EndHourCombo.SelectedItem.ToString().Split(':');

        var settings = new AjustesApp
        {
            FocusMinutes = focusMinutes,
            BreakMinutes = breakMinutes,
            Opacity = OpacitySlider.Value,
            IsDrastic = DrasticRadio.IsChecked == true,
            WorkStartTime = new TimeSpan(int.Parse(startParts[0]), int.Parse(startParts[1]), 0),
            WorkEndTime = new TimeSpan(int.Parse(endParts[0]), int.Parse(endParts[1]), 0)
        };

        _settingsService.SaveSettings(settings);
        _onSettingsSaved(settings);
        Close();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !int.TryParse(e.Text, out _);
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
