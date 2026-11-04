# Local Game Importer

A native Playnite add-on by **HyunCafe** for discovering installed Windows games in local folders. Version **0.3.2**.

## Why this extension exists

Playnite can manage games from many sources, but manually installed Windows games, standalone executables, and non-store libraries can still be tedious to add one by one. Local Game Importer was built for people whose games live in their own folders: select a scan directory, review detected launchers, and bulk-import selected titles into Playnite.

**Common use cases**

- **Playnite not detecting installed games:** Discover games stored outside Steam, Epic, GOG, or other linked libraries.
- **Batch-importing a game collection:** Scan multiple folders instead of manually creating a Playnite entry for every game.
- **Portable and standalone games:** Find local `.exe` launchers without a store installation record.
- **Repack-style folder names:** Suggest cleaner titles for certain naming patterns, including FitGirl/ElAmigos labels; manually review results.
- **Modded game launchers:** Review alternatives such as script extenders and explicitly select an appropriate launcher.

People searching for *"Playnite import local games"*, *"Playnite not finding installed games"*, *"bulk add non-Steam games to Playnite"*, or *"Playnite scan a folder for EXE games"* may find this tool useful.

This unofficial extension does not download or install games, bypass DRM, execute discovered installers during scanning, or automatically retrieve artwork. Its discovery results should be reviewed before importing.

## Install or update

1. Drag `LocalGameImporter-0.3.2.pext` into Playnite Desktop and follow its installation prompt. Restart Playnite.
2. Open **Main menu → Extensions → Local Games → Review games and automation**.
3. On a new installation, choose **Scan folders... → Add folder** and select the directories containing your games. There are no preconfigured paths. Up to 16 non-overlapping local folders can be scanned together.
4. Review titles and launchers, then click **Import checked**. Clear matches are checked initially. For another candidate, select it and click **Use selected launcher**.
5. Leave automatic imports enabled if desired. Each newly added scan folder waits for its first review before automatic import on startup/library refresh. Updating from 0.2 preserves folder preferences and exclusions but requires one review under the expanded discovery rules.

This release retains the extension GUID used by 0.2, so it updates the existing native add-on. If the older 0.1 script extension is still installed, disable it separately to avoid running two importers. Existing game entries are preserved.

## What it does

- Finds nested `.exe` launchers for standalone games without requiring store registration. Common repack labels and version suffixes are removed from proposed titles while editions remain distinct; titles remain editable.
- Shows a **File source** column containing the full configured scan-folder path. The source remains visible while scrolling; hover to read a long path.
- Skips known installer/checksum helpers, backup copies, and selected asset/mod-data trees. Store-client folders are distinguished from game folders.
- Recognizes common script extenders, alternate launchers and mod markers. Such matches require review. This is heuristic detection and does not inspect mod-manager profiles or certify compatibility.
- Lists `.bat` and `.cmd` wrappers for explicit manual selection. They are never imported automatically or executed during discovery. Their contents are hashed to detect changes between selection and import, not analyzed for safety.
- Preserves existing Playnite entries and their launch settings. Imported entries use normal file play actions, so they continue working if the add-on is removed.
- Provides status/reason and metadata columns, plus counts of added, existing, excluded, ready and unresolved rows.

## Faster manual review (v0.3.1)

- Click an **Add** checkbox once to toggle that game's inclusion. **Ctrl/Shift-click** game titles to select multiple rows. **Check selected** / **Uncheck selected** changes the import checkboxes, not launcher approval.
- Completed entries (`ADDED` and `EXISTING`) are hidden in the review list by default. **Show imported / existing games** reveals them as read-only rows. They are still counted in the summary and preserved in Playnite. `NAME MATCH` stays visible because a same-title match alone does not prove an identical installation.
- **Confirm checked EXEs...** opens a scrollable preview of the exact proposed launcher paths. Only explicitly checked regular EXE candidates can be bulk-approved. Read the paths and click Confirm to mark eligible rows `MANUAL`.
- Incomplete scans can be explicitly approved after verifying the listed EXE, but a manual decision does not certify a complete scan. `.bat`/`.cmd`, mod loaders, launcher wrappers, title conflicts, existing-library duplicates, and stale/unsafe choices remain held for individual review.
- **Import checked** shows the names and statuses of blocked games. If some checked entries are already `READY` or `MANUAL`, you can import only those confirmed entries while leaving the others unchanged. A partial import turns **automatic importing off**.
- After importing, use **Select imports for metadata** and Playnite's **Download metadata → Selected games only → Only download missing metadata** workflow.

Bulk confirmation is a user-directed review, not an automatic safety assessment of executables. Test important launchers in Playnite before relying on them. Game files are never launched while scanning or approving candidates.

## Ignoring games without deleting them

An **Ignore** checkbox is separate from **Add**. Tick the Ignore column for any unresolved entry (including utility or same-title rows), select **Move marked to ignored...** and confirm the full folder-path preview. The entry leaves the normal review list immediately. Open **Show ignored games (only)** to view the ignore section, tick its Ignore checkboxes, then choose **Restore marked ignored...**. Restore returns a candidate to review without importing it. Ignore decisions persist independently per configured scan folder across Playnite restarts and rescans, using the game-folder path rather than title. Ignored entries cannot be auto-imported. The extension never moves or deletes game files and never deletes Playnite library entries. Import and ignore checkboxes are intentionally separate to prevent accidents.

## Folder organization and incomplete results

Normally, each immediate child of a scan folder represents one game. If a child is a category containing several games, select its row and use **Mark / unmark category folder**. Its children are then scanned separately. Nested category marking is supported up to four levels and is saved per scan folder.

An **INCOMPLETE** result includes the actual issue category: a depth/directory/candidate/time limit, an inaccessible or linked path, a locked file, or another read failure. It remains ineligible for automatic import. Resolve the issue and rescan, or use **Browse known launcher...** to select a known working launcher inside that game folder. Manual selection does not mean the entire folder has been inspected.

Discovery is bounded to 2,000 enumerated game/category folders per root, 500 searched directories per game, six launcher-search levels, 200 candidates and 10,000 launch files per game. A root gets up to 120 seconds within a combined 180-second budget. Local synchronous file operations can delay cancellation while an individual operation is pending. A missing/offline root is reported separately; existing entries are never removed.

Excluded asset/mod-data directories are outside launcher discovery. This avoids treating a large asset tree as an incomplete launch search; unusual launchers in those trees may require the manual file chooser. Junctions/symlinks are not followed. Native executable hardlinks remain ordinary readable files.

## Launchers, mods and save-backup tools

Use the launcher and arguments already known to work for a modded game. The optional working-directory and tracking controls are under **Working directory and game-stop tracking**. A custom working directory must remain inside the game folder. Folder tracking uses the game directory and a two-second initial delay; check actual game exit behavior for unusual launchers.

The add-on does not deploy/purge mods, switch profiles, install/update games, run discovered programs, or change game/save files. External launch tools, PowerShell scripts and other arrangements that cannot be expressed as a local EXE/BAT/CMD plus arguments still require Playnite's normal action editor.

Existing save-backup add-ons, including Ludusavi, keep their settings. A correct library entry does not prove that a save-backup manifest recognizes its title or that a launcher is tracked correctly. Verify the first actual launch, exit and backup with the existing integration.

## Artwork and metadata

After each successful import, the new batch is selected in Playnite automatically. The importer never changes existing artwork, descriptions or other metadata.

Use **Select imports for metadata** to select entries created during the current Playnite session, then:

1. Open **Main menu → Library → Download metadata**.
2. Choose **Selected games only**.
3. Enable **Only download missing metadata** to preserve filled fields.
4. Include **Cover image**, **Background image**, **Description**, and the other standard fields you want. Playnite uses its configured metadata providers, such as IGDB.
5. Return to the importer and click **Refresh metadata status** to see remaining missing core fields.

The public SDK used by this release exposes selection and edit APIs but no direct entry point to start Playnite's bulk downloader. This workflow does not use UI keystroke injection, reflection into private Playnite code, or a separate scraper. Selection does not start a download.

Missing covers, backgrounds, descriptions and icons are flagged. Provider match ambiguity and artwork quality are **not exposed to this importer**: a populated field is never labeled a verified correct match. Resolve missing/uncertain titles or editions through the provider/game editor. The importer cannot enforce the downloader's options; keep the missing-only option enabled when preserving filled fields.

## Scope and privacy

This is a Windows-game launcher importer. Loose emulator ROMs use Playnite's emulator scanner; adding a ROM directory here does not configure an emulator or import ROMs.

The runtime performs no network requests, telemetry, credential/registry reads or game-file writes. It reads configured folders, launch-file headers/metadata, small command wrappers for hashing, and existing Playnite entry data. It writes its own local preferences and adds new Playnite entries. Configured paths/exclusions remain in Playnite's local extension-data directory; no inventory log is created. Public source/releases contain no local preferences.

Like any Playnite extension, it runs with Playnite's user permissions. It does not authenticate or malware-scan discovered games or command wrappers. The code's scope restrictions are not an operating-system sandbox.

## Validation and development

Target: **Playnite 10.63 / SDK 6.18.0 / .NET Framework 4.6.2**. The complete plugin compiles against the real SDK and framework references. Source tests use synthetic fixtures and an in-memory API/dispatcher. Live Windows loading, themed layout, launcher execution and provider downloads are not established by those tests.

See `BUILD.md`, `TESTING.md`, historical `AUDIT.md` and `PRIVACY-AUDIT.md`, and `PRIVACY-AUDIT-v0.3.2.md` for current release-candidate verification. This is custom unsigned software; no independent audit or code-signing certificate is claimed.

## License

Licensed under the [MIT License](LICENSE).
