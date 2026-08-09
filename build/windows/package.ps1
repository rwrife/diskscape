Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
$appProject = Join-Path $repoRoot "src/DiskScape.App/DiskScape.App.csproj"
$artifactRoot = Join-Path $repoRoot "artifacts"
$publishRoot = Join-Path $artifactRoot "publish/win-x64"
$releaseRoot = Join-Path $artifactRoot "release"
$msixRoot = Join-Path $artifactRoot "msix/"

New-Item -ItemType Directory -Force -Path $publishRoot | Out-Null
New-Item -ItemType Directory -Force -Path $releaseRoot | Out-Null
New-Item -ItemType Directory -Force -Path $msixRoot | Out-Null

Write-Host "Restoring projects..."
dotnet restore (Join-Path $repoRoot "diskscape.slnx")

Write-Host "Publishing self-contained win-x64 app..."
dotnet publish $appProject `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=false `
  -p:PublishReadyToRun=true `
  -o $publishRoot

$zipPath = Join-Path $releaseRoot "diskscape-win-x64.zip"
if (Test-Path $zipPath) {
  Remove-Item $zipPath -Force
}
Compress-Archive -Path (Join-Path $publishRoot "*") -DestinationPath $zipPath -Force

Write-Host "Publishing MSIX package..."
dotnet publish $appProject `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:WindowsPackageType=MSIX `
  -p:AppxBundle=Never `
  -p:UapAppxPackageBuildMode=SideloadOnly `
  -p:GenerateAppInstallerFile=false `
  -p:AppxPackageSigningEnabled=false `
  -p:AppxPackageDir=$msixRoot

$msix = Get-ChildItem -Path $msixRoot -Recurse -Filter *.msix | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $msix) {
  throw "MSIX package was not generated under $msixRoot"
}

$msixOut = Join-Path $releaseRoot "diskscape-win-x64.msix"
Copy-Item -Path $msix.FullName -Destination $msixOut -Force

Write-Host "Release artifacts ready:"
Write-Host " - $zipPath"
Write-Host " - $msixOut"
