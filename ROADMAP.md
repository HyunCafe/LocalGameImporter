# Local Game Importer — Development Roadmap

## Next revision — UI and reliability

- [ ] Single-click checkboxes: clicking a checkbox should immediately toggle it without first selecting the row.
- [ ] Improve failed-import diagnostics: show the specific game and reason when an import fails.
- [ ] Distinguish changed executable, invalid working directory, duplicate installation, and Playnite database errors.
- [ ] Provide a Retry failed imports action after rescanning or correcting launchers.
- [ ] Preserve checked selections and manual launcher choices when retrying where safe.
- [ ] Improve visual feedback for bulk-confirmed, skipped, and failed entries.

## Later improvements

- [ ] Reduce unnecessary INCOMPLETE results on large game directories.
- [ ] Improve launcher ranking for complex or modded installations.
- [ ] Streamline the metadata download workflow.
- [ ] Add a post-import results summary with per-game outcomes.

## Release safeguards

- Preserve previously imported games and existing Playnite actions.
- Do not execute unknown launchers or scripts during discovery.
- Keep automatic imports disabled for unresolved or unsafe candidates.
- Run automated and live Playnite tests before publishing a new release.
