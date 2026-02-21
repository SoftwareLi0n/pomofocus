using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FocusPomodoro.Models;
using FocusPomodoro.Services;

namespace FocusPomodoro;

public partial class HistoryWindow : Window
{
    private readonly SessionService _sessionService;
    private readonly Session? _currentSession;

    public HistoryWindow(SessionService sessionService, double opacity = 0.75, Session? currentSession = null)
    {
        InitializeComponent();
        _sessionService = sessionService;
        _currentSession = currentSession;
        ApplyOpacity(opacity);
        LoadData();
    }

    private void ApplyOpacity(double opacity)
    {
        var alpha = (byte)(opacity * 255);
        MainBorder.Background = new SolidColorBrush(Color.FromArgb(alpha, 26, 26, 46));
    }

    private void LoadData()
    {
        try
        {
            var allSessions = _sessionService.GetAllSessions();
            var currentSessionDisplay = GetCurrentSessionDisplay();
            
            var todaySessions = allSessions.Where(s => s.StartTime.Date == DateTime.Today).ToList();
            var todayTotalFocused = todaySessions.Sum(s => s.FocusedMinutes);
            var todayTotalDuration = todaySessions.Sum(s => s.TotalDurationMinutes);
            var todayCount = todaySessions.Count;
            
            if (currentSessionDisplay != null)
            {
                todayTotalFocused += currentSessionDisplay.FocusedMinutes;
                todayTotalDuration += currentSessionDisplay.TotalDurationMinutes;
                todayCount++;
            }
            
            var todayEfficiency = todayTotalDuration > 0 ? (todayTotalFocused * 100 / todayTotalDuration) : 0;

            TodaySessionsText.Text = todayCount.ToString();
            TodayEfficiencyText.Text = $"{todayEfficiency}%";

            var weekSummary = _sessionService.GetDailySummary(7);
            var weekTotalFocused = weekSummary.Values.Sum(v => v.focusedMinutes);
            var weekTotalDuration = weekSummary.Values.Sum(v => v.totalMinutes);
            WeekSummaryText.Text = $"{weekTotalFocused} min concentrado de {weekTotalDuration} min totales";

            var displaySessions = allSessions.Select(s => new SessionDisplay
            {
                StartTime = s.StartTime,
                TotalDurationMinutes = s.TotalDurationMinutes,
                FocusedMinutes = s.FocusedMinutes,
                Efficiency = s.TotalDurationMinutes > 0 ? (s.FocusedMinutes * 100 / s.TotalDurationMinutes) : 0,
                FocusSegments = s.FocusSegments ?? new List<FocusSegment>(),
                EfficiencyColor = GetEfficiencyColor(s.TotalDurationMinutes > 0 ? (s.FocusedMinutes * 100 / s.TotalDurationMinutes) : 0)
            }).ToList();

            if (currentSessionDisplay != null)
            {
                displaySessions.Insert(0, currentSessionDisplay);
            }

            SessionsListView.ItemsSource = displaySessions;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar datos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private SessionDisplay? GetCurrentSessionDisplay()
    {
        if (_currentSession == null) return null;

        var focusedMinutes = _currentSession.FocusSegments.Sum(s => s.TotalSeconds / 60);
        var efficiency = _currentSession.TotalDurationMinutes > 0 ? (focusedMinutes * 100 / _currentSession.TotalDurationMinutes) : 0;

        return new SessionDisplay
        {
            StartTime = _currentSession.StartTime,
            TotalDurationMinutes = _currentSession.TotalDurationMinutes,
            FocusedMinutes = focusedMinutes,
            Efficiency = efficiency,
            FocusSegments = _currentSession.FocusSegments ?? new List<FocusSegment>(),
            EfficiencyColor = GetEfficiencyColor(efficiency),
            IsInProgress = true
        };
    }

    private static Brush GetEfficiencyColor(int efficiency)
    {
        return efficiency switch
        {
            >= 80 => Brushes.LightGreen,
            >= 60 => Brushes.Gold,
            >= 40 => Brushes.Orange,
            _ => Brushes.Coral
        };
    }

    private void ClearHistoryBtn_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "¿Estás seguro de que deseas eliminar todo el historial?",
            "Confirmar eliminación",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning
        );

        if (result == MessageBoxResult.Yes)
        {
            _sessionService.ClearAllSessions();
            LoadData();
        }
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}

public class SessionDisplay
{
    public DateTime StartTime { get; set; }
    public int TotalDurationMinutes { get; set; }
    public int FocusedMinutes { get; set; }
    public int Efficiency { get; set; }
    public List<FocusSegment> FocusSegments { get; set; } = new();
    public int SegmentosCount => FocusSegments.Count;
    public bool HasSegments => FocusSegments.Count > 0;
    public string SegmentosDetalle => FocusSegments.Count > 0 
        ? string.Join(" | ", FocusSegments.Select((s, i) => $"#{i + 1}: {s.FormattedDuration}"))
        : "Sin registros";
    public Brush EfficiencyColor { get; set; } = Brushes.White;
    public bool IsInProgress { get; set; }
}
