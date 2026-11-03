$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
$source=Join-Path $project 'Source'
$testWork=Join-Path ([IO.Path]::GetTempPath()) ('LocalGameImporter-tests-'+[guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory $testWork
$hostSource=[IO.File]::ReadAllText((Join-Path $source 'Host.cs')).Replace('using System.Windows.Controls;','').Replace('using System.Windows.Data;','')
$boundary=$hostSource.IndexOf('        static Button Button(')
if($boundary -lt 0){throw 'Test adapter boundary not found.'}
$hostSource=$hostSource.Substring(0,$boundary)+"        static void ShowReview() { }`n        static void ShowFolders() { }`n    }`n}`n"
$hostSource=$hostSource.Replace('static Window window;','static Window window=null;')
[IO.File]::WriteAllText((Join-Path $testWork 'Host-test-adapter.cs'),$hostSource)
$runner=Join-Path $PSHOME $(if($IsWindows){'pwsh.exe'}else{'pwsh'})
& $runner -NoLogo -NoProfile -File (Join-Path $PSScriptRoot 'Scanner.Tests.ps1') -Source (Join-Path $source 'Scanner.cs') -Root (Join-Path $testWork 'scanner')
if($LASTEXITCODE -ne 0){throw 'Scanner tests failed.'}
& $runner -NoLogo -NoProfile -File (Join-Path $PSScriptRoot 'Host.Tests.ps1') -Base $testWork -Root (Join-Path $testWork 'host') -SourceDirectory $source
if($LASTEXITCODE -ne 0){throw 'Host tests failed.'}
Write-Host 'PASS: all scanner and simulated-host tests. Live Windows/WPF behavior is not tested by this suite.'
Write-Host ('Disposable test output: '+$testWork)
