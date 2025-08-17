param(
  [string]$ProjectFile = "AudioSFV.csproj",
  [string]$AppName = "AudioSFV",
  [string]$Configuration = "Release",
  [string]$Framework = "net8.0",
  [string]$Rid,
  [string]$OutputDir = "dist",
  [switch]$NoZip
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Resolve repo root and switch to it
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $root

# Determine RID if not provided
if (-not $Rid) {
  switch ($env:PROCESSOR_ARCHITECTURE) {
    'ARM64' { $Rid = 'win-arm64' }
    default { $Rid = 'win-x64' }
  }
}

Write-Host "Building $AppName for $Rid ($Configuration)..."
dotnet publish $ProjectFile -c $Configuration -r $Rid `
  -p:SelfContained=true -p:PublishSingleFile=false -p:PublishTrimmed=false `
  -p:UseAppHost=true -p:DebugType=None -p:DebugSymbols=false

$publishDir = Join-Path -Path "bin/$Configuration/$Framework/$Rid/publish" -ChildPath ''
if (-not (Test-Path $publishDir)) {
  throw "Publish directory not found: $publishDir"
}

# Prepare output
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

if (-not $NoZip) {
  $zipPath = Join-Path $OutputDir "$AppName.zip"
  if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
  Write-Host "Creating archive $zipPath..."
  Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath -Force
  Write-Host "Done: $zipPath"
}
else {
  if (Test-Path $OutputDir) { Remove-Item $OutputDir -Recurse -Force }
  New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
  Write-Host "Copying publish output to $OutputDir..."
  Copy-Item -Path (Join-Path $publishDir '*') -Destination $OutputDir -Recurse -Force
  Write-Host "Done: $OutputDir"
}
