# Public-release privacy audit — 0.3.0

## Result

The source distribution and installable extension were checked as actual ZIP archives, including their decompressed contents and the compiled DLL. No retained private identifiers, personal paths, credentials, local settings, logs, game inventories or backup data were found by the checks described below.

## Removed or replaced

- Personal names, handles, custom namespaces, notification identifiers and personalized author metadata.
- Hardcoded personal game locations and machine-specific defaults; new installations begin with no scan folders.
- Personalized instructions, directory examples, private configuration references and historical attachment details.
- Development workspace paths and local build/test output from the distributable files.

## Intentionally retained

- **HyunCafe**, the intended public publisher, in the manifest, README and assembly company metadata.
- Generic project/product names, version numbers and the opaque extension GUID needed for compatible updates.
- Public application/game names used by detection rules and synthetic tests. These are implementation cases, not an installed-game inventory.
- Public SDK/framework/compiler references, source/release hashes and official documentation links.
- Code that reads/migrates local preferences on the user's own computer. Those preferences are not included in either distribution.

## Checks performed

- Inspected all released source, scripts, examples, manifests and documentation for residual personal identifiers and paths.
- Scanned text and binary bytes in UTF-8 and both UTF-16 byte alignments for identifying strings, personal/workspace paths and common credential patterns.
- Reviewed matches for credential-related words; shipped ignore rules contain filenames/patterns, not secret values.
- Inspected PE/CLR assembly name, version, namespace, references, signing fields and debug directory. The namespace is generic; only a reproducible-build debug record is present. No CodeView path, embedded portable PDB or separate PDB is distributed.
- Rebuilt the DLL with the pinned inputs and compared the output byte-for-byte.
- Used an explicit archive allowlist; checked every archive member and content hash. No local settings, logs, fixtures, game/save/backup data, credential files, third-party runtime DLLs or private developer artifacts are included.
- Added `.gitignore` rules for settings, local data, credentials, logs, build outputs, temporary fixtures and editor state.

## Attention before publication

The artifacts are unsigned, and final Windows/Playnite runtime validation remains necessary. The supplied tests do not validate real launcher execution, UI rendering or metadata-provider matching. No independent security audit is claimed.

No distribution license has been chosen or included. Select the intended license before presenting the repository as open source. Public GitHub publishing has not been performed.

The ignore file cannot protect files already tracked by Git or future files added with force. Review the actual staged changes before publishing. Runtime folder paths shown in the UI and stored in local preferences are expected user data and should remain outside the repository.

These checks found no private data in this release; pattern-based checking cannot establish that every conceivable secret format has been excluded.
