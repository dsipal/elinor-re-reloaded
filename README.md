# elinor-re-reloaded
Trading tool for Eve Online. Export market data in game and Elinor shows the best buy/sell prices,
fees, profit and margin, and can copy an undercut price to the clipboard.

- Light and dark mode (Windows 11 Fluent style), following the Windows setting by default
- Compact always-on-top overlay for use over the EVE client (Ctrl+O)
- Per-profile skills, standings, broker fees, margins, order ranges, trade hubs and price step
- Shortcuts: Ctrl+A toggles auto copy, Ctrl+P keeps the window on top, Esc leaves the overlay

Downloads: https://github.com/dsipal/elinor-re-reloaded/releases (a single `Elinor.exe`, no install needed)

## History
A continuation of [Slivo-fr/elinor-reloaded](https://github.com/Slivo-fr/elinor-reloaded), which is no longer maintained,
itself based on the original Elinor by Virppi Jouhinen. This version moves the app to .NET 10, fixes the crashes on
Windows 11 and adds a modern UI. The full history of both projects is kept in this repository.

## Building

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) on Windows.

```
dotnet test Elinor-reloaded.sln
dotnet run --project Elinor
```

Portable single-file exe (no .NET install needed to run it):

```
dotnet publish Elinor -p:PublishProfile=win-x64
```

Output: `Elinor/bin/publish/Elinor.exe`. CI (`.github/workflows/build.yml`) builds this on every push and attaches it to `v*` tag releases.

## Where Elinor keeps its data

| What | Where |
|---|---|
| Settings | `%APPDATA%\Elinor\settings.json` |
| Profiles | `%APPDATA%\Elinor\profiles\*.json` |
| Error log | `%LOCALAPPDATA%\Elinor\logs\` |
| Market logs (read) | `Documents\EVE\logs\Marketlogs` (OneDrive-redirected Documents supported) |

On first start, profiles (`profiles\*.dat`) and settings (`user.config`) from Elinor 1.12 and earlier are imported automatically.

The update check reads `Elinor/currentVersion.xml` from the URL in `Elinor/Updates.cs`; point it at your fork before enabling it.
