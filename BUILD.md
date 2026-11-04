# Building Local Game Importer

The installed add-on does not require PowerShell. PowerShell is used only by the optional source-build and test scripts.

## Pinned build inputs

- Official PowerShell **7.6.9**, including Roslyn assembly version **5.0.0.0**.
- Official NuGet package **PlayniteSDK 6.18.0**: reference `lib/net462/Playnite.SDK.dll`.
- Microsoft NuGet package **Microsoft.NETFramework.ReferenceAssemblies.net462 1.0.3**: references under `build/.NETFramework/v4.6.2`.

Sources:

- https://github.com/PowerShell/PowerShell/releases/tag/v7.6.9
- https://www.nuget.org/packages/PlayniteSDK/6.18.0
- https://www.nuget.org/packages/Microsoft.NETFramework.ReferenceAssemblies.net462/1.0.3

Obtain and extract those inputs yourself. The build script does not download or install dependencies. No SDK/framework/compiler DLL is redistributed in the add-on.

## Compile

From PowerShell 7.6.9, supply the actual extracted reference paths:

```powershell
./Build.ps1 -FrameworkReferences '<extracted-framework>/build/.NETFramework/v4.6.2' -PlayniteSdkAssembly '<extracted-sdk>/lib/net462/Playnite.SDK.dll'
```

Default input: `Source/Scanner.cs`, `Source/Host.cs`, `Source/Plugin.cs`. Default output: `LocalGameImporter.dll` next to the script. Build failures do not write a partial output.

Compiler options: C# 5, release optimization, AnyCPU, deterministic DLL, assembly name `LocalGameImporter`. Source trees have no machine-specific file paths attached. The build emits no PDB/debug-symbol file. `BUILD-INPUTS.json` is a historical v0.3.0 record and does not verify v0.3.2.

## v0.3.2 Release build

From the repository root, with dependencies already restored:

```powershell
pwsh -NoLogo -NoProfile -File ./Tools/Verify-Dependencies.ps1
pwsh -NoLogo -NoProfile -File ./Tests/Run-Tests.ps1
dotnet build LocalGameImporter.csproj -c Release --no-restore
pwsh -NoLogo -NoProfile -File ./Tools/Package-Release.ps1
```

Release compilation explicitly uses `DebugType=none` and `DebugSymbols=false`.

## Package

A `.pext` is a ZIP with `extension.yaml` and `LocalGameImporter.dll` at its root. Do not bundle Playnite core/SDK DLLs, local settings, logs, fixtures, inventories, saves or credentials. The v0.3.2 package contains only the DLL, manifest and generated checksum file. Source and documentation remain in the GitHub repository.

`SHA256SUMS.txt` records each included file except itself. The packaging script verifies archived files against their expected hashes. No independent source-to-binary reproduction or security certification is claimed for v0.3.2. The binary is unsigned.

For a repository, commit the source project. The `.gitignore` excludes local outputs, settings and common sensitive files; attach the separately built `.pext` to a release when ready. Review staged files before publishing: ignore rules do not sanitize files that are already tracked.
