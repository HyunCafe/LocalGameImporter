# Offline packaging only. Requires a successfully built and independently tested DLL.
param([string]$DllPath='',[string]$OutputName='LocalGameImporter-0.3.1.pext')
if($OutputName -cnotmatch '^LocalGameImporter-0\.3\.1(?:-r[1-9][0-9]*)?\.pext$'){throw 'Unsupported output filename; use LocalGameImporter-0.3.1[-rN].pext'}
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if([string]::IsNullOrWhiteSpace($DllPath)){$DllPath=Join-Path $root 'bin/Release/net462/LocalGameImporter.dll'}
$DllPath=[IO.Path]::GetFullPath($DllPath)
if(-not (Test-Path -LiteralPath $DllPath -PathType Leaf)){throw "Build the v0.3.1 DLL first: $DllPath"}
$version=[Reflection.AssemblyName]::GetAssemblyName($DllPath).Version.ToString()
if($version -ne '0.3.1.0'){throw "Refusing to package unexpected assembly version: $version"}
$files=@('extension.yaml','README.md','CHANGELOG.md','AUDIT.md','PRIVACY-AUDIT.md','LICENSE',
    'Source/Host.cs','Source/Plugin.cs','Source/Scanner.cs')
foreach($file in $files){
    $path=Join-Path $root $file
    if(-not (Test-Path -LiteralPath $path -PathType Leaf)){throw "Missing file: $file"}
    if(([IO.File]::GetAttributes($path) -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw "Linked file not allowed: $file"}
}
if(-not (([IO.File]::ReadAllText((Join-Path $root 'extension.yaml'))) -match '(?m)^Version: 0\.3\.1\s*$')){throw 'Manifest version mismatch'}
$archiveFiles=[ordered]@{}
$archiveFiles['LocalGameImporter.dll']=[IO.File]::ReadAllBytes($DllPath)
foreach($file in $files){$archiveFiles[$file]=[IO.File]::ReadAllBytes((Join-Path $root $file))}
function Sha($bytes){
    $sha=[Security.Cryptography.SHA256]::Create()
    try {return [BitConverter]::ToString($sha.ComputeHash([byte[]]$bytes)).Replace('-','').ToLowerInvariant()}
    finally {$sha.Dispose()}
}
if((Sha $archiveFiles['LocalGameImporter.dll']) -eq '9013a1c9098b35d621c9d48d512d3b7e5bd00f1625aeed7e016e5dae1d759875'){
    throw 'DLL equals v0.3.0, not the new build'
}
$lines=foreach($name in ($archiveFiles.Keys | Sort-Object)){"$(Sha $archiveFiles[$name])  $name"}
$archiveFiles['SHA256SUMS.txt']=[Text.Encoding]::UTF8.GetBytes(($lines -join "`n")+"`n")
$dir=Join-Path (Split-Path $root -Parent) 'Releases/LocalGameImporter'
$null=New-Item -ItemType Directory -Force $dir
$out=Join-Path $dir $OutputName
if(Test-Path -LiteralPath $out){throw "Refusing to overwrite: $out"}
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.ZipFile
$stream=[IO.File]::Open($out,[IO.FileMode]::CreateNew,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
try {
    $zip=[IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Create,$true)
    try {
        foreach($name in ($archiveFiles.Keys | Sort-Object)) {
            $entry=$zip.CreateEntry($name,[IO.Compression.CompressionLevel]::Optimal)
            $dest=$entry.Open()
            try {$bytes=[byte[]]$archiveFiles[$name];$dest.Write($bytes,0,$bytes.Length)}
            finally {$dest.Dispose()}
        }
    } finally {$zip.Dispose()}
} finally {$stream.Dispose()}
# Reopen and verify every entry byte for byte. No user settings or game inventories are packaged.
try {
    $check=[IO.Compression.ZipFile]::OpenRead($out)
    try {
        if($check.Entries.Count -ne $archiveFiles.Count){throw 'Archive entry count mismatch'}
        foreach($entry in $check.Entries) {
            if(-not $archiveFiles.Contains($entry.FullName)){throw 'Unexpected archive file'}
            $read=$entry.Open();$buffer=New-Object IO.MemoryStream
            try {$read.CopyTo($buffer);$got=$buffer.ToArray()}
            finally {$read.Dispose();$buffer.Dispose()}
            if((Sha $got) -ne (Sha $archiveFiles[$entry.FullName])){throw "Archive hash mismatch: $($entry.FullName)"}
        }
    } finally {$check.Dispose()}
} catch {Remove-Item -LiteralPath $out -Force -ErrorAction SilentlyContinue;throw}
Write-Host "PASS: v$version package verified with $($archiveFiles.Count) files."
Write-Host "CREATED: $out"
Write-Host "SHA256: $(Sha ([IO.File]::ReadAllBytes($out)))"
Write-Host 'Local development package only. Re-test in Playnite before publishing.'
