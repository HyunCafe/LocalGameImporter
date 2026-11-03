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

Compiler options: C# 5, release optimization, AnyCPU, deterministic DLL, assembly name `LocalGameImporter`. Source trees have no machine-specific file paths attached. The build emits no PDB/debug-symbol file. Compare the output hash with `BUILD-INPUTS.json` for the supplied release.

## Package

A `.pext` is a ZIP with `extension.yaml` and `LocalGameImporter.dll` at its root. Do not bundle Playnite core/SDK DLLs, local settings, logs, fixtures, inventories, saves or credentials. This release also includes the exact C# source and brief documentation for inspection.

`SHA256SUMS.txt` records each included file except itself. The release process verifies those hashes and compares a second DLL build byte-for-byte with the distributed binary. This demonstrates repeatability for the pinned inputs, not independent security certification. The binary is unsigned.

For a repository, commit the source project. The `.gitignore` excludes local outputs, settings and common sensitive files; attach the separately built `.pext` to a release when ready. Review staged files before publishing: ignore rules do not sanitize files that are already tracked.
