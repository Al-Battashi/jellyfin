param(
    [switch]$All
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$workspace = Join-Path $root 'native/jellyfin-native'
$runtimes = Join-Path $root 'src/Jellyfin.NativeInterop/runtimes'

function Build-Target {
    param(
        [Parameter(Mandatory = $true)][string]$Triple,
        [Parameter(Mandatory = $true)][string]$Rid,
        [Parameter(Mandatory = $true)][string]$LibName
    )

    Write-Host "Building jf_native_abi for $Triple"
    cargo build --manifest-path "$workspace/Cargo.toml" --package jf_native_abi --release --target "$Triple"

    $sourcePath = Join-Path $workspace "target/$Triple/release/$LibName"
    $targetPath = Join-Path $runtimes "$Rid/native/$LibName"

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $targetPath) | Out-Null
    Copy-Item -Force $sourcePath $targetPath
    Write-Host "Copied $sourcePath -> $targetPath"
}

if ($All) {
    Build-Target -Triple 'x86_64-unknown-linux-gnu' -Rid 'linux-x64' -LibName 'libjf_native_abi.so'
    Build-Target -Triple 'aarch64-apple-darwin' -Rid 'osx-arm64' -LibName 'libjf_native_abi.dylib'
    Build-Target -Triple 'x86_64-pc-windows-msvc' -Rid 'win-x64' -LibName 'jf_native_abi.dll'
    exit 0
}

if ($IsWindows -and [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -eq [System.Runtime.InteropServices.Architecture]::X64) {
    Build-Target -Triple 'x86_64-pc-windows-msvc' -Rid 'win-x64' -LibName 'jf_native_abi.dll'
    exit 0
}

if ($IsLinux -and [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -eq [System.Runtime.InteropServices.Architecture]::X64) {
    Build-Target -Triple 'x86_64-unknown-linux-gnu' -Rid 'linux-x64' -LibName 'libjf_native_abi.so'
    exit 0
}

if ($IsMacOS -and [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -eq [System.Runtime.InteropServices.Architecture]::Arm64) {
    Build-Target -Triple 'aarch64-apple-darwin' -Rid 'osx-arm64' -LibName 'libjf_native_abi.dylib'
    exit 0
}

throw 'Unsupported host for automatic target mapping. Use -All or extend scripts/build-native-interop.ps1.'
