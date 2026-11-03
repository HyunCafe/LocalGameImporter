param(
    [Parameter(Mandatory=$true)][string]$FrameworkReferences,
    [Parameter(Mandatory=$true)][string]$PlayniteSdkAssembly,
    [string]$SourceDirectory=(Join-Path $PSScriptRoot 'Source'),
    [string]$OutputPath=(Join-Path $PSScriptRoot 'LocalGameImporter.dll')
)
# Optional source rebuild only. This is not needed to install or use the add-on.
# No download, dependency installer, game scan or Playnite database operation.
$ErrorActionPreference='Stop'
$refs=@('mscorlib.dll','System.dll','System.Core.dll','System.Xaml.dll','System.Xml.dll','System.Runtime.Serialization.dll','WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll') | ForEach-Object { Join-Path $FrameworkReferences $_ }
$refs+=$PlayniteSdkAssembly
$files=@('Scanner.cs','Host.cs','Plugin.cs') | ForEach-Object { Join-Path $SourceDirectory $_ }
foreach($inputPath in @($refs)+@($files)) {
    if(-not (Test-Path -LiteralPath $inputPath -PathType Leaf)){throw "Missing build input: $inputPath"}
}
Add-Type -Path (Join-Path $PSHOME 'Microsoft.CodeAnalysis.dll')
Add-Type -Path (Join-Path $PSHOME 'Microsoft.CodeAnalysis.CSharp.dll')
$references=[Microsoft.CodeAnalysis.MetadataReference[]]@($refs | ForEach-Object { [Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($_) })
$parse=[Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::Default.WithLanguageVersion([Microsoft.CodeAnalysis.CSharp.LanguageVersion]::CSharp5)
$trees=[Microsoft.CodeAnalysis.SyntaxTree[]]@($files | ForEach-Object { [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($_),$parse) })
$options=[Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions]::new([Microsoft.CodeAnalysis.OutputKind]::DynamicallyLinkedLibrary).WithOptimizationLevel([Microsoft.CodeAnalysis.OptimizationLevel]::Release).WithDeterministic($true)
$compilation=[Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create('LocalGameImporter').WithOptions($options).AddReferences($references).AddSyntaxTrees($trees)
$memory=New-Object IO.MemoryStream
try {
    $result=$compilation.Emit($memory)
    foreach($diagnostic in $result.Diagnostics) { Write-Host $diagnostic.ToString() }
    if(-not $result.Success){throw 'Compilation failed; output was not written.'}
    [IO.File]::WriteAllBytes([IO.Path]::GetFullPath($OutputPath),$memory.ToArray())
} finally { $memory.Dispose() }
Write-Host ('Built: '+$OutputPath)
Get-FileHash -LiteralPath $OutputPath -Algorithm SHA256
