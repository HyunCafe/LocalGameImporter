# Technical review record — 0.3.0

## Reviewed runtime scope

All three C# sources were reviewed. The add-on has no network client, process/shell execution, game-file deletion/write, credential access, registry access, private Playnite reflection, downloaded updater or background service. It reads configured launch locations and current Playnite entries, saves local preferences, adds new entries, selects imported entries and presents dialogs. Save failures disable automatic imports.

Existing entries are checked again before each add. The add-on has no existing-game update/delete call. Artwork and metadata are inspected only for empty core fields; the downloader itself remains Playnite's normal UI. The user must choose selected games and missing-only mode there. No unsupported command invocation or separate scraper is used.

Command-wrapper hashing detects changes; it does not establish trustworthy script contents. EXE revalidation checks location, link status, size, timestamp and headers. Same-size EXE replacement with spoofed timestamps is outside that check's assurance. Loader and folder-name rules are heuristics and may require manual correction.

## Build and validation

- Full source compilation against actual Playnite SDK 6.18.0 and Microsoft .NET Framework 4.6.2/WPF references.
- Scanner fixture and simulated host/lifecycle tests described in `TESTING.md`.
- Deterministic rebuild comparison and archive/hash validation.
- Actual release content and PE/CLR metadata inspection described in `PRIVACY-AUDIT.md`.

No real Windows Playnite instance or installed game collection was available for final 0.3.0 runtime testing. The fixture tests and compile do not certify live UI, tracking, metadata-provider behavior or independent security review.

## Primary references

- https://github.com/JosefNemec/Playnite/releases/tag/10.63
- https://github.com/JosefNemec/Playnite/blob/10.63/source/PlayniteSDK/Playnite.SDK.csproj
- https://api.playnite.link/docs/api/Playnite.SDK.IPlayniteAPI.html
- https://api.playnite.link/docs/api/Playnite.SDK.IMainViewAPI.html
- https://api.playnite.link/docs/api/Playnite.SDK.IGameDatabaseAPI.html
- https://api.playnite.link/docs/manual/library/games/gameActions.html
- https://github.com/JosefNemec/Playnite/blob/10.63/source/Playnite/Controllers/GenericGameController.cs
- https://github.com/JosefNemec/Playnite/blob/10.63/source/Playnite.DesktopApp/Windows/MetadataDownloadWindow.xaml
- https://github.com/JosefNemec/Playnite/blob/10.63/source/Playnite/Plugins/ExtensionInstaller.cs

Reference APIs and source were inspected for the specific behaviors used. No third-party runtime package is bundled.
