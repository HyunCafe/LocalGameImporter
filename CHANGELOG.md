# Changelog

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
