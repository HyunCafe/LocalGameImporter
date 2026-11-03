param([string]$Source,[string]$Root)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $Root){throw 'Use a new disposable fixture folder.'}
Add-Type -Path $Source
function Assert($b,$m){if(-not $b){throw "FAIL: $m"}}
function Pe($relative) {
 $p=Join-Path $Root $relative;$null=New-Item -ItemType Directory -Force ([IO.Path]::GetDirectoryName($p))
 $bytes=New-Object byte[] 256;$bytes[0]=0x4d;$bytes[1]=0x5a;$bytes[0x3c]=0x40;$bytes[0x40]=0x50;$bytes[0x41]=0x45;$bytes[0x56]=2
 [IO.File]::WriteAllBytes($p,$bytes);[IO.File]::SetLastWriteTimeUtc($p,[datetime]::UtcNow.AddDays(-3));return $p
}
function Request($root,$groups=@(),$excluded=@()) {
 $q=New-Object LocalGameImporter.ScanRequest;$q.Root=$root;$q.Groups=$groups;$q.Excluded=$excluded;return $q
}
$null=Pe 'DiskA/Simple Game/SimpleGame.exe'
$null=Pe 'DiskB/Another Game/AnotherGame.exe'
$ra=Join-Path $Root 'DiskA';$rb=Join-Path $Root 'DiskB'
$requests=[LocalGameImporter.ScanRequest[]]@((Request $ra),(Request $rb),(Request (Join-Path $Root 'Offline')))
$r=[LocalGameImporter.Scanner]::ScanMany($requests,@(),[Threading.CancellationToken]::None,0)
Assert (($r.Rows | Where-Object Status -eq 'READY').Count -eq 2) 'multiple roots discovered in one batch'
Assert ($r.RootErrors.Count -eq 1 -and ($r.Rows | Where-Object Status -eq 'ROOT ERROR').Count -eq 1) 'offline root reported separately'
Assert (($r.Rows | Where-Object Title -eq 'Another Game').ScanRoot -eq $rb) 'scan root attached to every game'
$null=Pe 'DiskB/Simple Game/SimpleGame.exe'
$r=[LocalGameImporter.Scanner]::ScanMany($requests,@(),[Threading.CancellationToken]::None,0)
Assert (($r.Rows | Where-Object { $_.Title -eq 'Simple Game' -and $_.Status -eq 'NAME MATCH' }).Count -eq 2) 'same-title conflicts across disks require review'
Write-Host 'PASS: multiple roots, independent offline-root reporting and cross-root title conflicts.'
$null=Pe 'Grouped/RPGs/One Game/OneGame.exe'
$null=Pe 'Grouped/RPGs/More/Second Game/SecondGame.exe'
$rg=Join-Path $Root 'Grouped';$groups=[string[]]@('RPGs','RPGs/More')
$r=[LocalGameImporter.Scanner]::Scan($rg,@(),@(),[Threading.CancellationToken]::None,0,$groups)
$one=$r.Rows | Where-Object Title -eq 'One Game';$two=$r.Rows | Where-Object Title -eq 'Second Game'
Assert ($one.Status -eq 'READY' -and $two.Status -eq 'READY') 'explicit category hierarchy yields game rows'
Assert (($r.Rows | Where-Object IsGroup).Count -eq 2) 'categories visible and not game entries'
Assert ([LocalGameImporter.Scanner]::Revalidate($rg,$two,$two.Choice,$groups)) 'grouped game allowed at import'
Assert (-not [LocalGameImporter.Scanner]::Revalidate($rg,$two,$two.Choice,@())) 'unmarked hierarchy fails revalidation'
$r=[LocalGameImporter.Scanner]::Scan($rg,@(),@('RPGs/More/Second Game'),[Threading.CancellationToken]::None,0,$groups)
Assert (($r.Rows | Where-Object Title -eq 'Second Game').Status -eq 'EXCLUDED') 'relative category exclusion retained'
Write-Host 'PASS: explicit nested categories, containment revalidation and scoped exclusions.'
$null=Pe 'Launches/Wrapper Game/WrapperGame.exe'
$wrapper=Join-Path $Root 'Launches/Wrapper Game/start_game.cmd';[IO.File]::WriteAllText($wrapper,'rem alpha')
$null=Pe 'Launches/Stardew Valley/Stardew Valley.exe';$null=Pe 'Launches/Stardew Valley/StardewModdingAPI.exe'
$null=Pe 'Launches/Skyrim VR/SkyrimVR.exe';$null=Pe 'Launches/Skyrim VR/sksevr_loader.exe'
$null=Pe 'Launches/Modded Game/ModdedGame.exe'
$null=New-Item -ItemType Directory -Force (Join-Path $Root 'Launches/Modded Game/BepInEx/plugins/deep/a/b/c/d/e/f')
$rl=Join-Path $Root 'Launches'
$r=[LocalGameImporter.Scanner]::Scan($rl,@(),@(),[Threading.CancellationToken]::None,0)
foreach($title in @('Wrapper Game','Stardew Valley','Skyrim VR','Modded Game')){Assert (($r.Rows | Where-Object Title -eq $title).Status -eq 'REVIEW') 'wrapper/loader/mod markers require review'}
$row=$r.Rows | Where-Object Title -eq 'Wrapper Game';$candidate=$row.Candidates | Where-Object ScriptWrapper
Assert ($candidate.Path -eq $wrapper) 'command wrapper listed without execution'
Assert ([LocalGameImporter.Scanner]::Revalidate($rl,$row,$candidate)) 'unchanged selected wrapper validates'
$stamp=[IO.File]::GetLastWriteTimeUtc($wrapper);[IO.File]::WriteAllText($wrapper,'rem omega');[IO.File]::SetLastWriteTimeUtc($wrapper,$stamp)
Assert (-not [LocalGameImporter.Scanner]::Revalidate($rl,$row,$candidate)) 'same-size/time wrapper content change detected'
Write-Host 'PASS: CMD discovery without execution, wrapper hash check, SMAPI/VR loaders and bounded mod-tree exclusions.'
$deep=Pe 'Limits/Deep Game/a/b/c/d/e/f/g/DeepGame.exe';$rd=Join-Path $Root 'Limits'
$r=[LocalGameImporter.Scanner]::Scan($rd,@(),@(),[Threading.CancellationToken]::None,0)
$row=$r.Rows | Where-Object Title -eq 'Deep Game'
Assert ($row.Status -eq 'INCOMPLETE' -and $row.Reason -match '6-level') 'incomplete report gives actual depth reason'
$c=[LocalGameImporter.Scanner]::ManualCandidate($rd,$row,$deep,@())
Assert ($null -ne $c -and [LocalGameImporter.Scanner]::Revalidate($rd,$row,$c)) 'known launcher can be selected despite bounded scan'
Assert ($null -eq [LocalGameImporter.Scanner]::ManualCandidate($rd,$row,$wrapper,@())) 'manual selection cannot leave game folder'
$row.Choice=$c;$row.WorkingDirectory='.'
Assert ([LocalGameImporter.Scanner]::LaunchDirectory($row) -eq $row.Folder) 'relative working directory supported'
$row.WorkingDirectory='..'
Assert ($null -eq [LocalGameImporter.Scanner]::LaunchDirectory($row)) 'working directory escape refused'
Write-Host 'PASS: specific incomplete reasons, scoped manual fallback and working-directory containment.'
$null=Pe 'Regressions/Loader Example/LoaderExample.exe'
$null=Pe 'Regressions/Loader Example/Backup/LoaderExample.exe'
$null=Pe 'Regressions/Loader Example/skse64_loader.exe'
$null=Pe 'Regressions/Checksum Example/RapidCRC.exe'
$null=Pe 'Regressions/GOG Galaxy/GalaxyClient Helper.exe'
$null=Pe 'Regressions/Asset Game/AssetGame.exe'
$null=Pe 'Regressions/Asset Game/Content/a/b/c/d/e/f/g/AssetGame.exe'
$rs=Join-Path $Root 'Regressions';$r=[LocalGameImporter.Scanner]::Scan($rs,@(),@(),[Threading.CancellationToken]::None,0)
$row=$r.Rows | Where-Object Title -eq 'Loader Example'
Assert ($row.Status -eq 'REVIEW' -and $row.Candidates.Count -eq 2 -and -not ($row.Candidates | Where-Object Relative -match 'Backup')) 'backup EXE excluded while current game and SKSE remain for review'
Assert (($r.Rows | Where-Object Title -eq 'Checksum Example').Candidates.Count -eq 0) 'RapidCRC is never a game candidate'
Assert (($r.Rows | Where-Object Title -eq 'GOG Galaxy').Status -eq 'UTILITY') 'store client distinguished from game'
Assert (($r.Rows | Where-Object Title -eq 'Asset Game').Status -eq 'READY') 'deep excluded asset tree no longer creates false incomplete launch scan'
Write-Host 'PASS: backup/checksum/store-client/asset-tree regressions.'
