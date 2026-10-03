using System;
using System.Windows;
using FocusPomodoro.Core.Services;

namespace FocusPomodoro.Features.Bloqueo;

/// <summary>
/// Ventana de bloqueo para fuera de horario de trabajo.
/// Usa ServicioBloqueoVentana para prevenir que el usuario la cierre.
/// </summary>
public partial class VentanaBloqueo : Window
{
    private readonly ServicioBloqueoVentana _bloqueo = new();

    public VentanaBloqueo(TimeSpan workStartTime)
    {
        InitializeComponent();

        SubtitleText.Text = $"Vuelve a las {workStartTime:hh\\:mm}";

        Loaded += (_, _) => _bloqueo.Activar(this);
    }

    /// <summary>
    /// Cierra la ventana de forma legítima (cuando vuelve el horario laboral).
    /// </summary>
    public void CerrarLegitimamente()
    {
        _bloqueo.PermitirCierre();
        _bloqueo.Dispose();
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _bloqueo.Dispose();
        base.OnClosed(e);
    }
}
