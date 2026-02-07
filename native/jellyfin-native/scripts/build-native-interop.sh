#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
WORKSPACE_DIR="$ROOT_DIR/native/jellyfin-native"
RUNTIME_DIR="$ROOT_DIR/src/Jellyfin.NativeInterop/runtimes"

build_target() {
  local triple="$1"
  local rid="$2"
  local libname="$3"

  echo "Building jf_native_abi for $triple"
  cargo build \
    --manifest-path "$WORKSPACE_DIR/Cargo.toml" \
    --package jf_native_abi \
    --release \
    --target "$triple"

  local source_path="$WORKSPACE_DIR/target/$triple/release/$libname"
  local target_path="$RUNTIME_DIR/$rid/native/$libname"

  mkdir -p "$(dirname "$target_path")"
  cp "$source_path" "$target_path"
  echo "Copied $source_path -> $target_path"
}

if [[ "${1:-}" == "--all" ]]; then
  build_target "x86_64-unknown-linux-gnu" "linux-x64" "libjf_native_abi.so"
  build_target "aarch64-apple-darwin" "osx-arm64" "libjf_native_abi.dylib"
  build_target "x86_64-pc-windows-msvc" "win-x64" "jf_native_abi.dll"
  exit 0
fi

os="$(uname -s)"
arch="$(uname -m)"

case "$os:$arch" in
  Linux:x86_64)
    build_target "x86_64-unknown-linux-gnu" "linux-x64" "libjf_native_abi.so"
    ;;
  Darwin:arm64)
    build_target "aarch64-apple-darwin" "osx-arm64" "libjf_native_abi.dylib"
    ;;
  MINGW64_NT-*:x86_64|MSYS_NT-*:x86_64|CYGWIN_NT-*:x86_64)
    build_target "x86_64-pc-windows-msvc" "win-x64" "jf_native_abi.dll"
    ;;
  *)
    echo "Unsupported host for automatic target mapping: $os/$arch" >&2
    echo "Use --all or add a mapping in scripts/build-native-interop.sh" >&2
    exit 1
    ;;
esac
