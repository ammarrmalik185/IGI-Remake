param(
    [string]$SourceRepository = (Join-Path $PSScriptRoot '..\..\IGIEnhanced'),
    [string]$GamePath,
    [switch]$FinalizeOnly
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$SourceRepository = (Resolve-Path -LiteralPath $SourceRepository).Path
if (-not $GamePath) { $GamePath = Join-Path $SourceRepository 'base_igi\pc' }
$GamePath = (Resolve-Path -LiteralPath $GamePath).Path
$extractor = Join-Path $SourceRepository 'tools\OpenIGI.Tools.ResExtract\bin\Release\net10.0\OpenIGI.Tools.ResExtract.dll'
$scripting = Join-Path $SourceRepository 'src\Core\OpenIGI.Scripting\bin\Release\net10.0\OpenIGI.Scripting.dll'
if (-not (Test-Path -LiteralPath $extractor) -or -not (Test-Path -LiteralPath $scripting)) {
    throw 'Build OpenIGI.Tools.ResExtract and OpenIGI.Scripting in IGIEnhanced first. The importer uses those local decoders without copying their code into Unity.'
}
New-Item -ItemType Directory -Force -Path (Join-Path $projectRoot 'Logs') | Out-Null
if (-not $FinalizeOnly) {
    & dotnet $extractor images $GamePath (Join-Path $projectRoot 'Assets\IGIImported\Textures') *> (Join-Path $projectRoot 'Logs\igi-textures.log')
    if ($LASTEXITCODE -ne 0) { throw 'Texture extraction failed. See Logs/igi-textures.log.' }
    & dotnet $extractor sounds $GamePath (Join-Path $projectRoot 'Assets\IGIImported\Audio') *> (Join-Path $projectRoot 'Logs\igi-audio.log')
    if ($LASTEXITCODE -ne 0) { throw 'Audio extraction failed. See Logs/igi-audio.log.' }
}
$arguments = @('run', '--project', (Join-Path $PSScriptRoot 'IGIImport\IGIImport.csproj'), ('-p:IgiSource=' + $SourceRepository), '--', $GamePath, $projectRoot)
if ($FinalizeOnly) { $arguments += '--finalize' }
& dotnet @arguments *> (Join-Path $projectRoot 'Logs\igi-models.log')
if ($LASTEXITCODE -ne 0) { throw 'Content conversion failed. See Logs/igi-models.log.' }
Write-Host 'Imported content is in Assets/IGIImported. In Unity, open Tools > IGI Rebuilt > Imported campaign.'
