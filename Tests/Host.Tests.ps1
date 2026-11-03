param([string]$Base,[string]$Root,[string]$SourceDirectory)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $Root){throw 'Use a new disposable fixture folder.'}
Add-Type -Path @((Join-Path $SourceDirectory 'Scanner.cs'),(Join-Path $Base 'Host-test-adapter.cs'),(Join-Path $PSScriptRoot 'Playnite.Stubs.cs'))
function Assert($b,$m){if(-not $b){throw "FAIL: $m"}}
$flags=[Reflection.BindingFlags]'NonPublic,Static'
$hostType=[LocalGameImporter.Host]
function SetField($n,$v){$hostType.GetField($n,$flags).SetValue($null,$v)}
function GetField($n){$hostType.GetField($n,$flags).GetValue($null)}
function FreshApi {
 SetField 'api' $null;SetField 'stopping' $false;SetField 'busy' $false;SetField 'wantReview' $false;SetField 'latest' $null;SetField 'lastStart' ([datetime]::MinValue)
 $a=New-Object Playnite.SDK.FakeAPI
 $dir=Join-Path $Base ('host-data-'+[guid]::NewGuid().ToString('N'))
 $null=New-Item -ItemType Directory -Force $dir
 @{Automatic=$true;ReviewPolicyVersion=3;Folders=@(@{Root=$Root;ReviewedRoot='';Excluded=@();GroupedFolders=@()})} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $dir 'settings.json')
 [LocalGameImporter.Host]::Initialize($a,$dir)
 $p=GetField 'prefs';$p.Folders[0].Root=$Root
 return $a
}
function Rows {
 [LocalGameImporter.Scanner]::Scan($Root,@(),@(),[Threading.CancellationToken]::None,0).Rows
}
function ImportRows($rows,$automatic) {
 $list=New-Object 'System.Collections.Generic.List[LocalGameImporter.GameRow]'
 foreach($row in $rows){$list.Add($row)}
 $parameters=New-Object object[] 2;$parameters[0]=$list.PSObject.BaseObject;$parameters[1]=[bool]$automatic
 $null=$hostType.GetMethod('Import',$flags).Invoke($null,$parameters)
}
function WaitCallback($a) {
 $sw=[Diagnostics.Stopwatch]::StartNew()
 while($a.MainView.UIDispatcher.Queue.IsEmpty -and $sw.Elapsed.TotalSeconds -lt 10){[Threading.Thread]::Sleep(20)}
 Assert (-not $a.MainView.UIDispatcher.Queue.IsEmpty) 'background completion enqueued'
}
$null=New-Item -ItemType Directory -Force (Join-Path $Root 'Demo Game')
$path=Join-Path $Root 'Demo Game/DemoGame.exe';$bytes=New-Object byte[] 256
$bytes[0]=0x4d;$bytes[1]=0x5a;$bytes[0x3c]=0x40;$bytes[0x40]=0x50;$bytes[0x41]=0x45;$bytes[0x56]=2
[IO.File]::WriteAllBytes($path,$bytes);[IO.File]::SetLastWriteTimeUtc($path,[datetime]::UtcNow.AddDays(-5));[IO.File]::SetCreationTimeUtc($path,[datetime]::UtcNow.AddDays(-5))
$a=FreshApi;$row=(Rows)[0];$row.Arguments='-known-working-argument'
ImportRows @($row) $false
Assert ($a.Database.Games.Count -eq 1) 'manual import adds entry'
Assert ($a.Database.Games[0].GameActions[0].Arguments -eq '-known-working-argument') 'explicit arguments retained'
Assert ($a.Database.Games[0].GameActions[0].Path -eq $path) 'selected executable used'
$old=$a.Database.Games[0];$old.Name='User edited title';ImportRows @((Rows)[0]) $false
Assert ($a.Database.Games.Count -eq 1 -and $old.Name -eq 'User edited title') 'rechecked path prevents duplicate and preserves edits'
$a=FreshApi;(GetField 'prefs').Folders[0].ReviewedRoot=[LocalGameImporter.Scanner]::Canonical($Root);$row=(Rows)[0];$row.Status='REVIEW';ImportRows @($row) $true
Assert ($a.Database.Games.Count -eq 0) 'automation refuses uncertain matches'
$a=FreshApi;$row=(Rows)[0];$a.Database.Games.ThrowAfterAdd=$true;ImportRows @($row) $false
Assert ($a.Database.Games.Count -eq 1 -and $row.Status -eq 'ADDED') 'successful add with downstream exception recognized'
$a=FreshApi;$row=(Rows)[0];[IO.File]::AppendAllText($path,'changed');ImportRows @($row) $false
Assert ($a.Database.Games.Count -eq 0 -and $row.Status -eq 'CHANGED') 'stale candidate not imported'
[IO.File]::WriteAllBytes($path,$bytes);[IO.File]::SetLastWriteTimeUtc($path,[datetime]::UtcNow.AddDays(-5));[IO.File]::SetCreationTimeUtc($path,[datetime]::UtcNow.AddDays(-5))
Write-Host 'PASS: import preserves working arguments/other entries, rechecks duplicates and changes, and handles partial add exceptions.'
# Batch review and manual import safeguards (pure host decisions).
$a=FreshApi;$row=(Rows)[0]
Assert ([LocalGameImporter.Host]::ImportEligible($row)) 'READY + checked is eligible'
$row.Status='REVIEW'
Assert (-not [LocalGameImporter.Host]::ImportEligible($row)) 'REVIEW is not importable without confirmation'
Assert ([LocalGameImporter.Host]::BulkEligible($row)) 'reviewed EXE candidate is bulk-confirmable only on explicit user action'
$row.Status='INCOMPLETE'
Assert ([LocalGameImporter.Host]::BulkEligible($row)) 'incomplete EXE can be explicitly approved with incomplete warning'
$row.Status='NAME MATCH'
Assert (-not [LocalGameImporter.Host]::BulkEligible($row)) 'same-title conflict cannot be bulk-confirmed'
$row.Status='EXCLUDED'
Assert ([LocalGameImporter.Host]::BulkEligible($row)) 'previously excluded EXE only by explicit checked approval'
$row.Choice.ScriptWrapper=$true
Assert (-not [LocalGameImporter.Host]::BulkEligible($row)) 'script wrapper cannot be bulk-confirmed'
$row.Choice.ScriptWrapper=$false;$row.Choice.ModLoader=$true
Assert (-not [LocalGameImporter.Host]::BulkEligible($row)) 'mod loader cannot be bulk-confirmed'
$row.Choice.ModLoader=$false;$row.Choice.LaunchWrapper=$true
Assert (-not [LocalGameImporter.Host]::BulkEligible($row)) 'launch wrapper cannot be bulk-confirmed'
$row.Choice.LaunchWrapper=$false;$row.Include=$false
Assert (-not [LocalGameImporter.Host]::BulkEligible($row)) 'unchecked EXE cannot be bulk-confirmed'
$row.Include=$true;$row.Status='REVIEW'
Assert (([LocalGameImporter.Host]::BlockedNames([LocalGameImporter.GameRow[]]@($row))) -match 'Demo Game \[REVIEW\]') 'blocked import shows offending name and status'
ImportRows @($row) $false
Assert ($a.Database.Games.Count -eq 0) 'direct manual import refuses unconfirmed REVIEW row'
# The review filters must hide completed games without losing their internal rows.
$row.Status='ADDED';$row.Include=$false
Assert (-not [LocalGameImporter.Host]::ReviewVisible($row,$false)) 'ADDED hidden by default'
Assert ([LocalGameImporter.Host]::ReviewVisible($row,$true)) 'ADDED visible when showing completed'
Assert (-not $row.CanToggleImport) 'ADDED checkbox disabled'
$row.Status='EXISTING'
Assert (-not [LocalGameImporter.Host]::ReviewVisible($row,$false)) 'EXISTING hidden by default'
Assert (-not $row.CanToggleImport) 'EXISTING checkbox disabled'
$row.Status='UTILITY'
Assert ([LocalGameImporter.Host]::ReviewVisible($row,$false) -and -not $row.CanToggleImport) 'utility visible but cannot be selected for import'
$row.Status='INCOMPLETE'
Assert ([LocalGameImporter.Host]::ReviewVisible($row,$false) -and $row.CanToggleImport) 'unfinished game remains available for review'
$row.IsGroup=$true
Assert (-not $row.CanToggleImport) 'category row checkbox disabled'
$row.IsGroup=$false
$row.IsRoot=$true
Assert (-not $row.CanToggleImport) 'scan root checkbox disabled'
$row.IsRoot=$false
# Persistent Ignore queue: separate from Add selection, per-root, and reversible.
$a=FreshApi;$row=(Rows)[0];$row.MarkIgnore=$true
Assert ($row.CanMarkIgnore) 'normal game may be marked for ignore'
Assert (-not $row.Ignored) 'new game begins visible'
$count=[LocalGameImporter.Host]::SetIgnoredForMarked([LocalGameImporter.GameRow[]]@($row),$true)
Assert ($count -eq 1 -and $row.Ignored -and -not $row.Include -and -not $row.MarkIgnore) 'ignore clears Add and selection'
Assert (-not [LocalGameImporter.Host]::ReviewVisible($row,$false,$false)) 'ignored game hidden from default review'
Assert ([LocalGameImporter.Host]::ReviewVisible($row,$false,$true)) 'ignored-only view shows row'
Assert (-not [LocalGameImporter.Host]::ImportEligible($row) -and -not [LocalGameImporter.Host]::BulkEligible($row)) 'ignored candidate cannot import'
$row.Include=$true
ImportRows @($row) $false
Assert ($a.Database.Games.Count -eq 0) 'direct ignored manual row cannot import even with Add checked'
$row.Include=$false
$p=GetField 'prefs';$key=[LocalGameImporter.Scanner]::FolderKey($Root,$row.Folder)
Assert ($p.Folders[0].Ignored.Contains($key)) 'ignored key saved per configured scan root'
$p.Folders[0].ReviewedRoot=[LocalGameImporter.Scanner]::Canonical($Root)
[LocalGameImporter.Host]::StartupOrRefresh();WaitCallback $a;$a.MainView.UIDispatcher.Drain()
Assert ($a.Database.Games.Count -eq 0) 'reviewed scan root never auto imports an ignored game'
# Same title on another scan root must not be ignored by a title-level rule.
$another=Join-Path $Base ('different-drive-'+[guid]::NewGuid().ToString('N'))
$extra=New-Object LocalGameImporter.FolderPreferences;$extra.Root=$another;$p.Folders.Add($extra)
$other=(Rows)[0];$other.ScanRoot=$another;$other.Folder=Join-Path $another 'Demo Game'
$scan=New-Object LocalGameImporter.ScanResult
$scan.Rows.Add($row);$scan.Rows.Add($other)
$parameters=New-Object 'object[]' 1
$parameters[0]=$scan.PSObject.BaseObject
$null=$hostType.GetMethod('ApplyIgnored',$flags).Invoke($null,$parameters)
Assert ($row.Ignored -and -not $other.Ignored) 'ignore key is isolated by root despite matching game titles'
$pathToSettings=GetField 'dataPath';SetField 'api' $null
[LocalGameImporter.Host]::Initialize($a,$pathToSettings)
Assert ((GetField 'prefs').Folders[0].Ignored.Contains($key)) 'ignored state survives settings reload'
$row.MarkIgnore=$true
$count=[LocalGameImporter.Host]::SetIgnoredForMarked([LocalGameImporter.GameRow[]]@($row),$false)
Assert ($count -eq 1 -and -not $row.Ignored -and -not $row.Include) 'restoring makes a candidate visible but does not import'
Assert (-not (GetField 'prefs').Folders[0].Ignored.Contains($key)) 'restore removes persisted ignore key'
$row.Status='UTILITY';$row.MarkIgnore=$true
Assert ($row.CanMarkIgnore -and -not $row.CanToggleImport) 'utility may be ignored without becoming importable'
$row.Status='EXISTING';$row.MarkIgnore=$false
Assert (-not $row.CanMarkIgnore) 'existing Playnite entries cannot be ignored or altered'
Write-Host 'PASS: persistent per-root ignored list, reversible view, and import protections.'
Write-Host 'PASS: completed-row filter, disabled checkboxes, and batch EXE gates.'
Write-Host 'PASS: bulk EXE gates, blocked-game diagnostics, and unconfirmed manual import guard.'

$a=FreshApi
[LocalGameImporter.Host]::StartupOrRefresh();WaitCallback $a;$a.MainView.UIDispatcher.Drain()
Assert ($a.Database.Games.Count -eq 0) 'first startup only discovers'
$a=FreshApi;$p=GetField 'prefs';$p.Folders[0].ReviewedRoot=[LocalGameImporter.Scanner]::Canonical($Root)
[LocalGameImporter.Host]::StartupOrRefresh();WaitCallback $a;$a.MainView.UIDispatcher.Drain()
Assert ($a.Database.Games.Count -eq 1) 'reviewed root auto imports clear match'
$a=FreshApi;$p=GetField 'prefs';$p.Folders[0].ReviewedRoot=[LocalGameImporter.Scanner]::Canonical($Root)
[LocalGameImporter.Host]::StartupOrRefresh();WaitCallback $a;[LocalGameImporter.Host]::Cancel();$a.MainView.UIDispatcher.Drain()
Assert ($a.Database.Games.Count -eq 0) 'late cancellation prevents queued import'
$a=FreshApi;$p=GetField 'prefs';$p.Folders[0].ReviewedRoot=[LocalGameImporter.Scanner]::Canonical($Root)
[LocalGameImporter.Host]::StartupOrRefresh();WaitCallback $a;[LocalGameImporter.Host]::Stop();$a.MainView.UIDispatcher.Drain()
Assert ($a.Database.Games.Count -eq 0) 'shutdown prevents queued import'
Write-Host 'PASS: startup review gate, reviewed-root automation, late cancellation and shutdown with a simulated Playnite dispatcher.'
$a=FreshApi;$p=GetField 'prefs';$p.Folders[0].ReviewedRoot=[LocalGameImporter.Scanner]::Canonical($Root);$p.Folders[0].Excluded.Add('Keep excluded')
$null=$hostType.GetMethod('Save',$flags).Invoke($null,@())
$savedPath=GetField 'dataPath';SetField 'api' $null
[LocalGameImporter.Host]::Initialize($a,$savedPath)
Assert ([LocalGameImporter.Host]::AutomaticEnabled()) 'reviewed root and automation persist'
Assert ((GetField 'prefs').Folders[0].Excluded.Contains('Keep excluded')) 'exclusion persists'
SetField 'dataPath' ''
$saveFailed=$false
try { $null=$hostType.GetMethod('Save',$flags).Invoke($null,@()) }
catch { $saveFailed=$true }
Assert $saveFailed 'missing settings path reports failure'
Assert (-not [LocalGameImporter.Host]::AutomaticEnabled()) 'save validation failure disables automation'
Write-Host 'PASS: preferences round-trip and save validation failure disables automation.'
$a=FreshApi
$existing=New-Object Playnite.SDK.Models.Game;$existing.Name='Already configured';$existing.CoverImage='existing-cover';$existing.BackgroundImage='existing-background';$existing.Description='existing-description';$existing.Icon='existing-icon'
$a.Database.Games.Add($existing)
$row=(Rows)[0];ImportRows @($row) $false
Assert ($a.MainView.Selected.Count -eq 1 -and $a.MainView.Selected[0] -eq $row.ImportedId) 'only newly added batch automatically selected'
Assert ($existing.CoverImage -eq 'existing-cover' -and $existing.Description -eq 'existing-description') 'existing artwork and metadata unchanged'
Assert ($row.MetadataStatus -match 'Missing: cover, background, description, icon') 'missing metadata reported separately'
$g=$a.Database.Games.Get($row.ImportedId);$g.CoverImage='provider-cover';$g.BackgroundImage='provider-background';$g.Description='provider-description';$g.Icon='provider-icon'
Assert ([LocalGameImporter.Host]::MetadataState($g) -match 'Core fields present; provider match not verified') 'filled metadata not falsely certified as correct match'
Assert ([LocalGameImporter.Host]::ImportedGames().Count -eq 1) 'metadata handoff limited to entries created in this session'
Write-Host 'PASS: automatic new-batch selection, existing metadata preservation and honest missing/match status.'
$a=FreshApi;$wrapper=Join-Path $Root 'Demo Game/start_game.cmd';[IO.File]::WriteAllText($wrapper,'rem test wrapper only')
$row=(Rows)[0];$row.Choice=$row.Candidates | Where-Object ScriptWrapper;$row.Status='MANUAL';$row.Include=$true;$row.Tracking='Folder';$row.WorkingDirectory='.'
ImportRows @($row) $false
$action=$a.Database.Games[0].GameActions[0]
Assert ($action.Path -eq $wrapper -and $action.TrackingMode -eq 'Directory' -and $action.TrackingPath -eq $row.Folder -and $action.WorkingDir -eq $row.Folder) 'manual wrapper launch/tracking settings retained'
$a=FreshApi;(GetField 'prefs').Folders[0].ReviewedRoot=[LocalGameImporter.Scanner]::Canonical($Root)
$row=(Rows)[0];$row.Choice=$row.Candidates | Where-Object ScriptWrapper;$row.Status='READY';ImportRows @($row) $true
Assert ($a.Database.Games.Count -eq 0) 'wrapper cannot be automatically imported even with READY status'
Assert ([IO.File]::ReadAllText($wrapper) -eq 'rem test wrapper only') 'wrapper unchanged'
Write-Host 'PASS: explicit command-wrapper setup, folder tracking, and enforced no automatic wrapper import.'
$legacy=Join-Path $Base ('legacy-'+[guid]::NewGuid().ToString('N'));$null=New-Item -ItemType Directory $legacy
@{Root=$Root;Automatic=$true;ReviewedRoot=$Root;Excluded=@('Remember me')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $legacy 'settings.json')
SetField 'api' $null;[LocalGameImporter.Host]::Initialize($a,$legacy);$p=GetField 'prefs'
Assert ($p.Folders.Count -eq 1 -and $p.Folders[0].Root -eq $Root -and $p.Folders[0].Excluded.Contains('Remember me')) '0.2 root and exclusions migrate'
Assert (-not [LocalGameImporter.Host]::AutomaticEnabled()) 'upgrade asks one review under new rules'
Write-Host 'PASS: 0.2 settings migration preserves root/exclusions and rearms first review.'
$a=FreshApi;$p=GetField 'prefs'
$first=Join-Path $Base ('multi-first-'+[guid]::NewGuid().ToString('N'));$second=Join-Path $Base ('multi-second-'+[guid]::NewGuid().ToString('N'))
foreach($pair in @(@($first,'First Game','FirstGame.exe'),@($second,'Second Game','SecondGame.exe'))) {
 $dir=Join-Path $pair[0] $pair[1];$null=New-Item -ItemType Directory -Force $dir;$file=Join-Path $dir $pair[2]
 [IO.File]::WriteAllBytes($file,$bytes);[IO.File]::SetLastWriteTimeUtc($file,[datetime]::UtcNow.AddDays(-5));[IO.File]::SetCreationTimeUtc($file,[datetime]::UtcNow.AddDays(-5))
}
$p.Folders[0].Root=$first;$p.Folders[0].ReviewedRoot=$first
$extra=New-Object LocalGameImporter.FolderPreferences;$extra.Root=$second;$p.Folders.Add($extra)
$offline=New-Object LocalGameImporter.FolderPreferences;$offline.Root=Join-Path $Base 'does-not-exist-drive';$offline.ReviewedRoot=$offline.Root;$p.Folders.Add($offline)
[LocalGameImporter.Host]::StartupOrRefresh();WaitCallback $a;$a.MainView.UIDispatcher.Drain()
Assert ($a.Database.Games.Count -eq 1 -and $a.Database.Games[0].Name -eq 'First Game') 'reviewed root imports while unreviewed root waits and offline root is isolated'
Assert ((GetField 'latest').RootErrors.Count -eq 1) 'host retains offline root report'
$extra.ReviewedRoot=$second;SetField 'lastStart' ([datetime]::MinValue)
[LocalGameImporter.Host]::StartupOrRefresh();WaitCallback $a;$a.MainView.UIDispatcher.Drain()
Assert ($a.Database.Games.Count -eq 2) 'second root imports after its review without duplicating first'
Write-Host 'PASS: independent root review state, offline-root isolation and repeated multi-root deduplication.'
SetField 'api' $null;SetField 'stopping' $false
$firstRun=Join-Path $Base ('first-run-'+[guid]::NewGuid().ToString('N'))
$emptyApi=New-Object Playnite.SDK.FakeAPI
[LocalGameImporter.Host]::Initialize($emptyApi,$firstRun)
Assert ((GetField 'prefs').Folders.Count -eq 0) 'new installations have no hardcoded scan folders'
[LocalGameImporter.Host]::StartupOrRefresh()
Assert ($emptyApi.Database.Games.Count -eq 0 -and $emptyApi.Notifications.Last -match 'choose your game folders') 'first run prompts setup without scanning personal default paths'
Write-Host 'PASS: empty first-run setup has no personal defaults or automatic imports.'
