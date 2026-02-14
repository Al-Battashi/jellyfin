# Start Jellyfin (Exact Commands)

This guide is command-focused and assumes you are running from the server repository root.

## 1) Build the web client once

```bash
cd Client/jellyfin-web-master
npm ci
npm run build:production
cd ../..
```

## 2) Start server + hosted web client (macOS/Linux)

```bash
cd /absolute/path/to/jellyfin
dotnet run --project Jellyfin.Server --configuration Debug -- --webdir "$PWD/Client/jellyfin-web-master/dist"
```

Server URL:

```text
http://localhost:8096
```

## 3) Start server + hosted web client (Windows PowerShell)

```powershell
cd C:\Dev\jellyfin
dotnet run --project Jellyfin.Server --configuration Debug -- --webdir "$PWD\Client\jellyfin-web-master\dist"
```

Server URL:

```text
http://localhost:8096
```

## 4) Start with explicit FFmpeg path (recommended in dev)

### macOS/Linux

```bash
cd /absolute/path/to/jellyfin
dotnet run --project Jellyfin.Server --configuration Debug -- --webdir "$PWD/Client/jellyfin-web-master/dist" --ffmpeg "/absolute/path/to/ffmpeg"
```

### Windows PowerShell

```powershell
cd C:\Dev\jellyfin
dotnet run --project Jellyfin.Server --configuration Debug -- --webdir "$PWD\Client\jellyfin-web-master\dist" --ffmpeg "C:\Dev\FFmpeg\ffmpeg.exe"
```

## 5) Start with dedicated data/cache folders

### macOS/Linux

```bash
cd /absolute/path/to/jellyfin
mkdir -p .jf-run/data .jf-run/cache
dotnet run --project Jellyfin.Server --configuration Debug -- --datadir "$PWD/.jf-run/data" --cachedir "$PWD/.jf-run/cache" --webdir "$PWD/Client/jellyfin-web-master/dist" --ffmpeg "/absolute/path/to/ffmpeg"
```

### Windows PowerShell

```powershell
cd C:\Dev\jellyfin
mkdir .jf-run\data -Force | Out-Null
mkdir .jf-run\cache -Force | Out-Null
dotnet run --project Jellyfin.Server --configuration Debug -- --datadir "$PWD\.jf-run\data" --cachedir "$PWD\.jf-run\cache" --webdir "$PWD\Client\jellyfin-web-master\dist" --ffmpeg "C:\Dev\FFmpeg\ffmpeg.exe"
```

## 6) Run prebuilt publish output (Windows EXE)

After publishing, run:

```powershell
cd C:\path\to\publish\server
.\jellyfin.exe --datadir C:\Dev\jf-data --cachedir C:\Dev\jf-cache --webdir C:\Dev\jellyfin-web\dist --ffmpeg C:\Dev\FFmpeg\ffmpeg.exe
```

## 7) Run server without hosting web client

```bash
cd /absolute/path/to/jellyfin
dotnet run --project Jellyfin.Server --configuration Debug -- --nowebclient
```

Use a separate web client dev server (`npm start` in `Client/jellyfin-web-master`) or another client app to connect.
