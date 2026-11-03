# Changelog

## v0.3.1 development — persistent ignore queue (r3)
- Added per-scan-root persistent Ignored queue independent of automatic import exclusions.
- Added separate one-click Ignore checkboxes, explicit move/restore actions, full path previews and an ignored-only view.
- Ignored game folders never import automatically, are hidden from the default review and remain recoverable without touching game files.
- Added regression tests for persistence, restoring, root-specific paths and import safeguards.

## 0.3.1 (development candidate)

- Make import checkboxes toggle on the first click, without requiring a row to enter editing mode.
- Hide completed (`ADDED`) and already represented (`EXISTING`) games by default; provide a read-only Show imported / existing games toggle without losing metadata selection or counts.
- Keep same-title but unverified (`NAME MATCH`) games visible and unchecked; no automatic duplicate suppression by title alone.
- Allow Ctrl/Shift multi-row selection and Check selected / Uncheck selected commands.
- Add explicit scrollable confirmation for checked EXE launchers, with per-game paths and status.
- Hold scripts, mod loaders, changed candidates, same-title conflicts and existing-library duplicates for individual review.
- Replace generic all-or-nothing import error with named blockers and an optional partial import of validated games.
- Disable automation on partial import; preserve prior exclusions, configuration, and existing game entries.
- No additional network access, launcher execution during scanning, or automatic metadata download.

## 0.3.0

- Add up to 16 simultaneous scan folders with independent first-review state and exclusions.
- Add a full-path File source column, category-folder marking and clearer per-root/per-game reports.
- Report specific incomplete-scan causes; add a scoped manual launcher chooser.
- Exclude backup copies, checksum tools and selected asset/mod-data trees; distinguish common store-client folders.
- Expand loader/mod markers and support explicitly chosen EXE/BAT/CMD launchers with arguments, working-directory and game-stop tracking controls.
- Automatically select newly imported batches; provide the supported handoff to Playnite's bulk metadata downloader and missing-field reporting.
- Preserve existing entries, artwork, metadata and external mod/backup configuration.
- Use theme text resources for explanatory labels.
- Replace personal defaults/identifiers with a generic project namespace, public publisher metadata and empty first-run folder setup.
- Preserve the native extension GUID and migrate existing local settings without distributing those settings.
- Add public build/test documentation, source tests, ignore rules and a packaged-file privacy audit.

## 0.2.0

- Introduce the native plugin package, background discovery, initial review and optional startup/library-refresh imports.
- Add duplicate/path checks, candidate review, cancellation and source/build validation.

## 0.1.0

- Initial script-based manual bulk-import prototype.
