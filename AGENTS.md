# AGENTS.md

## Build & Run
```bash
dotnet build          # Debug build
dotnet run            # Run debug version
dotnet publish -c Release -r win-x64 --self-contained true -o bin/Release/net8.0-windows/win-x64/publish  # Release build
```

## Generate Installer
Requires Inno Setup 6 at `C:\Users\Lion\AppData\Local\Programs\Inno Setup 6\ISCC.exe`:
```bash
& "C:\Users\Lion\AppData\Local\Programs\Inno Setup 6\ISCC.exe" "D:\projects\projects-lion\FocusPomodoro\installer.iss"
```
Output: `installer_output/Soldado_Setup_1.0.0.exe`

## Key Facts
- **Assembly name**: `Soldado` (not FocusPomodoro) - defined in `.csproj`
- **Data location**: `%APPDATA%\FocusPomodoro\` (JSON files for sessions/settings)
- **No tests** configured
- **Language**: Interface in Spanish

## Architecture
- WPF .NET 8.0-windows desktop app
- Windows code-behind pattern (XAML + .cs files)
- BreakWindow uses Win32 `SetWindowsHookEx` keyboard hooks to block Alt+Tab, Win key, etc. during breaks

## Quirks
- When rebuilding after changes to csproj AssemblyName, delete `bin/Release` folder first to avoid caching old exe
- Installer script references `Soldado.exe` but publishes `Soldado` as assembly name - verify match in both places