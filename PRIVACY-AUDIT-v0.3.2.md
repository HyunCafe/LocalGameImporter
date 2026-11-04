# Local Game Importer v0.3.2 — Privacy Verification

Date: November 4, 2026
Status: Local release candidate; not independent certification.

## Verified

- Pinned NuGet archives passed the existing checksum verification.
- Automated scanner and simulated-host tests passed.
- Release compilation succeeded for .NET Framework 4.6.2.
- Effective Release settings: DebugType=none; DebugSymbols=false.
- No matches were found for six checked debug/path/identifier signatures in the Release DLL.
- Assembly version is 0.3.2.0; extension manifest version is 0.3.2.
- The package contains exactly LocalGameImporter.dll, extension.yaml and SHA256SUMS.txt.
- The packaging script verified archive contents and checksums.

## Artifact hashes

DLL SHA-256:
9984f1feb121d2cda8b572ad9d61fd615000f25822d06b43e80a9f499e36e97c

Package SHA-256:
e9316082baa38030a74ef89289893fd1f489d6ef5ea573511fe9d64ceefad36e

## Reason for maintenance release

An earlier compiled DLL exposed a local development path through debug metadata.
The Release configuration was corrected and the replacement DLL passed
targeted privacy checks.

## Limitations

- Signature scans do not prove that all possible sensitive information is absent.
- No independent source-to-binary reproduction has been established.
- No third-party security audit or code signing is claimed.
- Synthetic tests do not establish live Playnite installation, UI,
  game launch, or metadata-provider functionality.
- Live Playnite regression testing remains required before publication.
- Final staged Git files and release artifacts require review.

Historical AUDIT.md and PRIVACY-AUDIT.md describe v0.3.0
and are not certification of v0.3.2.