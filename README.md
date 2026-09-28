# elinor-re-reloaded
Trading tool for Eve Online. Export market data in game and Elinor shows the best buy/sell prices,
fees, profit and margin, and can copy an undercut price to the clipboard.

- Light and dark mode (Windows 11 Fluent style), following the Windows setting by default
- Compact always-on-top overlay for use over the EVE client (Ctrl+O)
- Per-profile skills, standings, broker fees, margins, order ranges, trade hubs and price step
- Shortcuts: Ctrl+A toggles auto copy, Ctrl+P keeps the window on top, Esc leaves the overlay

Downloads: https://github.com/dsipal/elinor-re-reloaded/releases
- `Elinor-Setup-x.y.z.exe`: installer (per-user, no admin needed; Start menu shortcut, uninstaller)
- `Elinor.exe`: portable, just run it

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

Output: `Elinor/bin/publish/Elinor.exe`.

Installer (needs [Inno Setup 6](https://jrsoftware.org/isinfo.php)), after publishing:

```
iscc /DAppVersion=1.0.0 installer\Elinor.iss
```

## Releasing

Push a tag like `v1.0.1`. CI (`.github/workflows/build.yml`) builds the exe and installer with that version and
publishes a GitHub release with both. To rebuild the assets of an existing tag, run the workflow manually
(Actions > build > Run workflow) with the tag name. Bump `Elinor/currentVersion.xml` on master so the in-app
update check sees the new version.

## Where Elinor keeps its data

| What | Where |
|---|---|
| Settings | `%APPDATA%\Elinor\settings.json` |
| Profiles | `%APPDATA%\Elinor\profiles\*.json` |
| Error log | `%LOCALAPPDATA%\Elinor\logs\` |
| Market logs (read) | `Documents\EVE\logs\Marketlogs` (OneDrive-redirected Documents supported) |

On first start, profiles (`profiles\*.dat`) and settings (`user.config`) from Elinor 1.12 and earlier are imported automatically.

The update check reads `Elinor/currentVersion.xml` from the URL in `Elinor/Updates.cs`; point it at your fork before enabling it.
