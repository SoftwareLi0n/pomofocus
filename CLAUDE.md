# CLAUDE.md

Este archivo proporciona orientacion a Claude Code (claude.ai/code) al trabajar con el codigo de este repositorio.

## Descripcion del Proyecto

FocusPomodoro es una aplicacion de escritorio WPF tipo Pomodoro, orientada a .NET 8.0-windows. La interfaz esta en español.

## Compilar y Ejecutar

```bash
dotnet build
dotnet run
```

Salida: `bin/Debug/net8.0-windows/FocusPomodoro.exe`

No hay framework de pruebas configurado.

## Arquitectura

**Ventanas (UI + code-behind):**
- `MainWindow` — Temporizador principal con botones de inicio/pausa, configuracion, historial y donacion. Siempre visible encima, arrastrable y semi-transparente.
- `BreakWindow` — Temporizador de descanso a pantalla completa con hooks globales de teclado (P/Invoke `SetWindowsHookEx`) que bloquean Alt+Tab, Alt+F4, tecla Win y Ctrl+Esc durante los descansos.
- `SettingsWindow` — Configura duracion de enfoque/descanso y opacidad.
- `HistoryWindow` — Historial de sesiones, resumenes diarios y estadisticas de eficiencia.

**Modelos (`Models/`):**
- `Session` — Sesion de enfoque con lista de `FocusSegment`. `SessionData` es el wrapper raiz de serializacion.
- `Settings` (`AppSettings`) — Minutos de enfoque/descanso, opacidad por defecto.

**Servicios (`Services/`):**
- `SessionService` — CRUD en JSON para sesiones, resumenes diarios y calculos de eficiencia.
- `SettingsService` — Carga/guardado en JSON de preferencias del usuario.

Los datos se persisten como archivos JSON (`sessions.json`, `settings.json`) en `%APPDATA%\FocusPomodoro\`.

## Patrones Clave

- Temporizador controlado por `DispatcherTimer` con intervalos de 1 segundo.
- Colores de estado: Cyan (listo), Verde (enfocado), Dorado (descanso), Rojo (distraido).
- Umbrales de color de eficiencia: >80% verde, 60-80% dorado, 40-60% naranja, <40% coral.
- La ventana de descanso usa un hook de teclado de bajo nivel Win32 para forzar el tiempo de descanso.
