# Jellyfin Server Repo Developer Guide

This repo contains the Jellyfin **server** backend. It is a .NET solution with multiple libraries, a web API host, tests, and tooling. This guide annotates the project layout, how to build and run the server, and the conventions/tools you need to work effectively in this codebase.

## Quick Start (Local)

1. Install the .NET SDK specified by `global.json` (currently `10.0.0`).
2. Install ffmpeg (or point Jellyfin to a specific ffmpeg binary).
3. Obtain or build the Jellyfin web client (see below).
4. Run the server.

```bash
# From repo root
# Option 1: run with a web client directory

dotnet run --project Jellyfin.Server --webdir /absolute/path/to/jellyfin-web/dist

# Option 2: run without hosting the web client

dotnet run --project Jellyfin.Server --nowebclient
```

Default URL when hosting the web client: `http://localhost:8096`.

## Repo Layout (Annotated)

Paths are relative to the repo root.

- `Jellyfin.Server/` – The **server host** entrypoint (ASP.NET Core). See `Jellyfin.Server/Program.cs` and `Jellyfin.Server/Startup.cs`. This is the main project you run.
- `Jellyfin.Api/` – Web API controllers, models, middleware, and API infrastructure.
- `Jellyfin.Server.Implementations/` – Jellyfin-specific server implementations (users, security, storage helpers, etc.).
- `Emby.Server.Implementations/` – Legacy/compat server implementation modules used by Jellyfin.
- `Jellyfin.Data/` – Core data contracts and shared models. Versioned as a NuGet package.
- `MediaBrowser.*` – Core shared libraries (model, controller, providers, metadata, media encoding, etc.). Many server subsystems depend on these.
- `Emby.Naming/` – File naming and parsing utilities (legacy naming layer).
- `Emby.Photos/` – Photo-related utilities.
- `Client/` – Local checkout of the Jellyfin web client (frontend). In this workspace it lives at `Client/jellyfin-web-master/`.
- `src/` – Additional libraries and infra used by the server:
- `src/Jellyfin.CodeAnalysis/` – Custom Roslyn analyzers used during Debug builds.
- `src/Jellyfin.Database/` – Database abstractions and providers, including EF migrations.
- `src/Jellyfin.Drawing/` and `src/Jellyfin.Drawing.Skia/` – Image tooling (Skia-backed implementation).
- `src/Jellyfin.Extensions/` – Shared extension helpers.
- `src/Jellyfin.LiveTv/` – Live TV components.
- `src/Jellyfin.MediaEncoding.Hls/` and `src/Jellyfin.MediaEncoding.Keyframes/` – Media encoding utilities and HLS support.
- `src/Jellyfin.Networking/` – Networking utilities.
- `tests/` – Unit/integration tests for most modules.
- `fuzz/` – Fuzz testing harnesses and scripts.
- `.devcontainer/` – Devcontainer setup for Codespaces/containers.
- `.vscode/` – VS Code launch/tasks/extensions recommendations.
- `.github/` – GitHub workflows, templates, and code ownership.
- `deployment/` – Deployment-related assets (e.g., unraid templates).
- `Directory.Build.props` – Root build defaults and analyzer wiring.
- `Directory.Packages.props` – Central package version management.
- `global.json` – .NET SDK version pin.
- `SharedVersion.cs` – Shared assembly version used across projects.
- `stylecop.json` and `BannedSymbols.txt` – Code style and banned API configuration.

## SyncPlay Documentation

For a detailed SyncPlay implementation map (client + server), see:

- `docs/syncplay/README.md` – architecture, state/revision model, file-by-file responsibilities, and troubleshooting/test checklist.

## Prerequisites

- **.NET SDK**: `10.0.0` (from `global.json`).
- **Target framework**: `net10.0` is used throughout (see `Jellyfin.Server/Jellyfin.Server.csproj` and `tests/Directory.Build.props`).
- **ffmpeg**: required for media processing.
- **Web client**: Jellyfin server can host a static build of `jellyfin-web`.
- **Node.js + npm** (only if you build the web client locally): Node `>= 24`, npm `>= 11` (see `Client/jellyfin-web-master/package.json`).

Note: The root `README.md` mentions .NET 9, while `global.json` and project files target .NET 10. If you follow the repo strictly, use the version pinned in `global.json`.

## Web Client (jellyfin-web)

The web UI is usually maintained in its own repository. In this workspace there is a local copy under `Client/jellyfin-web-master/`. You have three options:

1. Download a prebuilt web client build from Jellyfin’s CI artifacts.
2. Build `jellyfin-web` from source separately.
3. Copy the web client build from an existing Jellyfin installation.

When running, pass `--webdir /absolute/path/to/jellyfin-web/dist`. If you don’t want the server to host the web UI, use `--nowebclient`.

### Local Web Client (this workspace)

The frontend is available at `Client/jellyfin-web-master/`. Typical workflows:

**Run dev server (fast iteration):**

```bash
cd Client/jellyfin-web-master
npm install
npm start
```

This runs a webpack dev server (usually `http://localhost:8080`). Use the UI to connect to your local Jellyfin server (`http://localhost:8096`). If you hit CORS issues, add the dev server origin to the Jellyfin CORS settings in the admin dashboard.

**Build and host from Jellyfin:**

```bash
cd Client/jellyfin-web-master
npm install
npm run build:development
```

Then run the server with:

```bash
dotnet run --project Jellyfin.Server --webdir /absolute/path/to/Client/jellyfin-web-master/dist
```

## Running the Server

Use `Jellyfin.Server` as the startup project.

```bash
# Run from the repo root

dotnet run --project Jellyfin.Server --webdir /absolute/path/to/jellyfin-web/dist
```

Relevant CLI options are defined in `Jellyfin.Server/StartupOptions.cs`:

- `--datadir` (`-d`): data directory (database files, etc.).
- `--cachedir` (`-C`): cache directory.
- `--configdir` (`-c`): configuration directory.
- `--logdir` (`-l`): log directory.
- `--webdir` (`-w`): web client directory.
- `--nowebclient`: don’t host web UI.
- `--ffmpeg`: path to ffmpeg binary.
- `--service`: headless service mode.
- `--package-name`: packaging integration.
- `--published-server-url`: URL to publish for auto-discover.
- `--nonetchange`: disable network change detection.
- `--restore-archive`: restore from a backup archive.

You can also run the built DLL or executable from `Jellyfin.Server/bin/Debug/net10.0/`.

## Windows Self-Contained Publish (Server + Web, FFmpeg External)

Use the publish script to produce a Windows `win-x64` self-contained output that includes the web client but does **not** bundle FFmpeg:

```powershell
pwsh ./scripts/publish-win-server-web.ps1
```

Useful options:

- `-Runtime` (default: `win-x64`)
- `-Configuration` (default: `Release`)
- `-OutputPath` (default: `artifacts/publish/<runtime>`)
- `-SkipWebBuild` (reuse existing `Client/jellyfin-web-master/dist`)
- `-SkipNpmInstall` (skip `npm ci` during web build)

Output layout:

- `artifacts/publish/win-x64/server/` (server publish output)
- `artifacts/publish/win-x64/server/jellyfin-web/` (copied web build)

FFmpeg requirement is unchanged:

- Provide `ffmpeg` in system `PATH`, or
- Launch with `--ffmpeg <path-to-ffmpeg.exe>`.

## VS Code + Launch Profiles

See `jellyfin.code-workspace` and `.vscode/launch.json`.

Available launch profiles:

- `.NET Launch (console)` – normal launch.
- `.NET Launch (nowebclient)` – web client disabled.
- `ghcs .NET Launch (nowebclient, ffmpeg)` – web client disabled, explicit ffmpeg path (for Codespaces).
- `.NET Attach` – attach to a running process.

Task shortcuts are in `.vscode/tasks.json`:

- `build` – `dotnet build Jellyfin.Server/Jellyfin.Server.csproj`.
- `api tests` – `dotnet test tests/Jellyfin.Api.Tests/Jellyfin.Api.Tests.csproj`.

## Devcontainer (Codespaces/Containers)

`/.devcontainer/devcontainer.json` defines a container with:

- Base image: `mcr.microsoft.com/devcontainers/dotnet:9.0-bookworm`
- Post-start: `dotnet restore`, `dotnet workload update`, HTTPS dev-certs, and ffmpeg install via `.devcontainer/install-ffmpeg.sh`
- Extensions installed from `.vscode/extensions.json`

Note: The container image targets .NET 9 while `global.json` expects .NET 10. If you use Codespaces, verify the SDK version inside the container matches the repo’s requirements.

## Logging

Default Serilog configuration lives at `Jellyfin.Server/Resources/Configuration/logging.json` and uses:

- Console output with timestamp + thread id
- Rolling file logs written to `%JELLYFIN_LOG_DIR%//log_.log`

## Database and EF Migrations

See `src/Jellyfin.Database/readme.md` for EF Core migration instructions.

Key points:

- Jellyfin supports multiple database providers, with SQLite as the current default.
- Migrations are provider-specific.
- Example for SQLite:

```bash
dotnet ef migrations add MIGRATION_NAME --project "src/Jellyfin.Database/Jellyfin.Database.Providers.Sqlite" -- --migration-provider Jellyfin-SQLite
```

## Tests

- All tests live under `tests/`.
- Run all tests:

```bash
dotnet test
```

- Coverage configuration: `tests/coverletArgs.runsettings`.

Test projects include unit tests, controller tests, integration tests, and module-specific suites such as:

- `tests/Jellyfin.Api.Tests/`
- `tests/Jellyfin.Server.Integration.Tests/`
- `tests/Jellyfin.MediaEncoding.Tests/`
- `tests/Jellyfin.Providers.Tests/`

## Fuzzing

The `fuzz/` directory contains fuzz harnesses and scripts. See `fuzz/README.md` to install AFL++ and run fuzzing scripts for specific projects.

## Code Style and Analyzers

- `Directory.Build.props` and `src/Directory.Build.props` wire up analyzers for Debug builds.
- `stylecop.json` and `.editorconfig` define formatting and style rules.
- `BannedSymbols.txt` bans specific APIs (for example, `Task<T>.Result` and Guid equality operators).

## Package Management

- Central package versioning: `Directory.Packages.props`.
- NuGet sources: `nuget.config` restricts to `https://api.nuget.org/v3/index.json`.

## Versioning and Release Helpers

- `SharedVersion.cs` holds the shared assembly version.
- `bump_version` script updates the shared version and relevant package versions, and stages the changes for commit.

## CI and Automation

GitHub Actions workflows live in `.github/workflows/` and cover:

- Tests and compatibility checks
- OpenAPI generation checks
- CodeQL analysis
- Release bump workflows and automation

## Common “Where Do I Find X?”

- **Server entrypoint**: `Jellyfin.Server/Program.cs` and `Jellyfin.Server/Startup.cs`
- **API controllers**: `Jellyfin.Api/Controllers/`
- **Server implementations**: `Jellyfin.Server.Implementations/` and `Emby.Server.Implementations/`
- **Domain models/contracts**: `MediaBrowser.Model/` and `Jellyfin.Data/`
- **Providers and metadata**: `MediaBrowser.Providers/`, `MediaBrowser.LocalMetadata/`, `MediaBrowser.XbmcMetadata/`
- **Media encoding**: `MediaBrowser.MediaEncoding/`, `src/Jellyfin.MediaEncoding.*`
- **Live TV**: `src/Jellyfin.LiveTv/`
- **Database providers**: `src/Jellyfin.Database/`
- **Static web assets**: `Jellyfin.Server/wwwroot/`

## Notes and Pitfalls

- Watch for version mismatches between `global.json`, `README.md`, and `.devcontainer`.
- The web client is a separate repo and needs to be built or downloaded.
- Tests and analyzers are strict in Debug builds; warnings are treated as errors (see `Directory.Build.props`).

## Suggested First Steps for New Contributors

1. Install the correct .NET SDK and ffmpeg.
2. Run the server with `--nowebclient` first to verify the backend starts.
3. Add the web client build and confirm `http://localhost:8096` loads.
4. Run `dotnet test` and ensure a clean baseline.
5. Explore `Jellyfin.Api/Controllers/` and `Jellyfin.Server.Implementations/` to orient yourself in the code.
