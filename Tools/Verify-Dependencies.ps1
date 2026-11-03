# Verify the exact official NuGet packages used for the published v0.3.0 build.
# Run after dotnet restore, before building. Fails closed on mismatches.
$ErrorActionPreference='Stop'
$cache=if($env:NUGET_PACKAGES){$env:NUGET_PACKAGES}else{Join-Path $env:USERPROFILE '.nuget/packages'}
$packages=@(
    @('playnitesdk/6.18.0/playnitesdk.6.18.0.nupkg','63ae943bf226a782738d3d6b9977dcb77861e5d2ea1aadcf620332618c93e236'),
    @('microsoft.netframework.referenceassemblies.net462/1.0.3/microsoft.netframework.referenceassemblies.net462.1.0.3.nupkg','ee692a845743500910855d4c330ca6e9ef87c16e16f740e4474d187185d66e21')
)
foreach($entry in $packages){
    $file=Join-Path $cache $entry[0]
    if(-not (Test-Path -LiteralPath $file -PathType Leaf)){throw "Dependency archive not in NuGet cache: $file"}
    $actual=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
    if($actual -ne $entry[1]){throw "Package archive SHA-256 mismatch: $($entry[0]). Do not build until reviewed."}
    Write-Host "PASS: $($entry[0])"
}
Write-Host 'PASS: pinned official NuGet archives match the verified v0.3.0 build inputs.'
