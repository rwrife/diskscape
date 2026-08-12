Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
$appProject = Join-Path $repoRoot "src/DiskScape.App/DiskScape.App.csproj"
$wapProject = Join-Path $repoRoot "src/DiskScape.Package/DiskScape.Package.wapproj"
$artifactRoot = Join-Path $repoRoot "artifacts"
$publishRoot = Join-Path $artifactRoot "publish/win-x64"
$releaseRoot = Join-Path $artifactRoot "release"
$wapOutputRoot = Join-Path $repoRoot "src/DiskScape.Package/AppPackages"

New-Item -ItemType Directory -Force -Path $publishRoot | Out-Null
New-Item -ItemType Directory -Force -Path $releaseRoot | Out-Null

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

if (-not (Get-Command msbuild -ErrorAction SilentlyContinue)) {
  throw "msbuild is required for MSIX packaging but was not found in PATH"
}

# Use a known-good UWP SDK by default (installed in CI), with an explicit
# override for local packaging environments.
$resolvedSdkVersion = if ($env:DISKSCAPE_WINDOWS_SDK_VERSION) {
  $env:DISKSCAPE_WINDOWS_SDK_VERSION
} else {
  "10.0.19041.0"
}

Write-Host "Building MSIX package with wapproj using Windows SDK $resolvedSdkVersion..."
msbuild $wapProject `
  /restore `
  /p:Configuration=Release `
  /p:Platform=x64 `
  /p:RuntimeIdentifier=win-x64 `
  /p:TargetPlatformIdentifier=Windows `
  /p:TargetPlatformVersion=$resolvedSdkVersion `
  /p:TargetPlatformMinVersion=10.0.17763.0 `
  /p:UapAppxPackageBuildMode=SideloadOnly `
  /p:AppxBundle=Never `
  /p:GenerateAppInstallerFile=false `
  /p:AppxPackageSigningEnabled=false

$msix = Get-ChildItem -Path $wapOutputRoot -Recurse -Filter *.msix | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $msix) {
  throw "MSIX package was not generated under $wapOutputRoot"
}

$msixOut = Join-Path $releaseRoot "diskscape-win-x64.msix"
Copy-Item -Path $msix.FullName -Destination $msixOut -Force

Write-Host "Release artifacts ready:"
Write-Host " - $zipPath"
Write-Host " - $msixOut"
