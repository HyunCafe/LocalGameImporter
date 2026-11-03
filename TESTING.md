# Validation

Run the included fixture tests from PowerShell 7.6.9:

```powershell
./Tests/Run-Tests.ps1
```

The runner creates a new randomly named temporary directory and launches isolated test processes. Fixtures contain synthetic executable headers and inert command-wrapper text. They are never executed as games. No real Playnite database, game folder, mod profile or backup destination is used.

Scanner cases cover multiple roots, offline roots, cross-root title conflicts, explicitly marked category folders, scoped exclusions, command-wrapper content changes, mod-loader detection, incomplete-scan reasons, manual launcher containment, backup/checksum filtering and excluded asset trees.

Host cases cover preserved existing entries/arguments/metadata, commit-time duplicate and file-change checks, partial add exceptions, startup review requirements, cancellation/shutdown, settings persistence, legacy settings migration, independent root approval, newly imported game selection, metadata missing-field reporting, wrapper/tracking settings and an empty first-run configuration.

The host harness strips the WPF review controls and substitutes an in-memory SDK/dispatcher. It does not validate real WPF binding/rendering or Playnite execution. The full, unmodified plugin is compiled separately against the actual Playnite SDK and .NET Framework/WPF references.

Before public release, validate on Windows in Playnite Desktop: installation/update, first-run setup, multiple folders, review controls, theme readability, representative launcher execution/exit tracking, and the selected-games/missing-only metadata workflow. The actual provider match and any separate save-backup result require real application checks.
