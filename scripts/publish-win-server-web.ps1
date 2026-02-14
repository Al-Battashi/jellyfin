param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$WebProjectPath = "",
    [string]$ServerProjectPath = "",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputPath = "",
    [switch]$SkipWebBuild,
    [switch]$SkipNpmInstall
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($WebProjectPath)) {
    $WebProjectPath = Join-Path $RepoRoot "Client/jellyfin-web-master"
}

if ([string]::IsNullOrWhiteSpace($ServerProjectPath)) {
    $ServerProjectPath = Join-Path $RepoRoot "Jellyfin.Server/Jellyfin.Server.csproj"
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $RepoRoot "artifacts/publish/$Runtime"
}

$webDistPath = Join-Path $WebProjectPath "dist"
$publishPath = Join-Path $OutputPath "server"
$publishWebPath = Join-Path $publishPath "jellyfin-web"

Write-Host "Repo root: $RepoRoot"
Write-Host "Web project: $WebProjectPath"
Write-Host "Server project: $ServerProjectPath"
Write-Host "Publish output: $publishPath"

if (-not $SkipWebBuild) {
    if (-not (Test-Path $WebProjectPath)) {
        throw "Web project path not found: $WebProjectPath"
    }

    Push-Location $WebProjectPath
    try {
        if (-not $SkipNpmInstall) {
            Write-Host "Installing web dependencies with npm ci..."
            npm ci
        }

        Write-Host "Building web client..."
        npm run build:production
    }
    finally {
        Pop-Location
    }
}

if (-not (Test-Path $webDistPath)) {
    throw "Web dist folder not found: $webDistPath. Build the web client first."
}

Write-Host "Publishing Jellyfin server..."
dotnet publish $ServerProjectPath `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    --output $publishPath

if (Test-Path $publishWebPath) {
    Remove-Item $publishWebPath -Recurse -Force
}

New-Item -ItemType Directory -Path $publishWebPath -Force | Out-Null
Copy-Item (Join-Path $webDistPath "*") $publishWebPath -Recurse -Force

Write-Host ""
Write-Host "Publish completed."
Write-Host "Output: $publishPath"
Write-Host "FFmpeg is not bundled. Keep ffmpeg available in PATH or launch with --ffmpeg <path-to-ffmpeg.exe>."
