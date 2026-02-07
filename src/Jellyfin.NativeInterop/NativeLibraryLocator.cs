using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Jellyfin.NativeInterop;

internal static class NativeLibraryLocator
{
    private const string LibraryBaseName = "jf_native_abi";

    static NativeLibraryLocator()
    {
        NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, ResolveLibrary);
    }

    private static nint ResolveLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, LibraryBaseName, StringComparison.Ordinal))
        {
            return nint.Zero;
        }

        foreach (var candidate in GetCandidatePaths())
        {
            if (NativeLibrary.TryLoad(candidate, out var handle))
            {
                return handle;
            }
        }

        return NativeLibrary.TryLoad(libraryName, assembly, searchPath, out var fallbackHandle)
            ? fallbackHandle
            : nint.Zero;
    }

    internal static void EnsureResolverInitialized()
    {
        // Force static ctor.
    }

    private static IEnumerable<string> GetCandidatePaths()
    {
        string libraryFileName = GetPlatformLibraryFileName();

        var envPath = Environment.GetEnvironmentVariable("JELLYFIN_NATIVE_LIBRARY_PATH");
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            if (Directory.Exists(envPath))
            {
                yield return Path.Combine(envPath, libraryFileName);
            }
            else
            {
                yield return envPath;
            }
        }

        var baseDirectory = AppContext.BaseDirectory;
        yield return Path.Combine(baseDirectory, libraryFileName);

        var rid = GetRuntimeIdentifier();
        if (!string.IsNullOrEmpty(rid))
        {
            yield return Path.Combine(baseDirectory, "runtimes", rid, "native", libraryFileName);
        }

        // Developer fallback path when running from source tree.
        var repoRoot = FindRepositoryRoot(baseDirectory);
        if (!string.IsNullOrEmpty(repoRoot))
        {
            yield return Path.Combine(repoRoot, "native", "jellyfin-native", "target", "release", libraryFileName);
            if (!string.IsNullOrEmpty(rid))
            {
                yield return Path.Combine(repoRoot, "src", "Jellyfin.NativeInterop", "runtimes", rid, "native", libraryFileName);
            }
        }
    }

    private static string GetPlatformLibraryFileName()
    {
        if (OperatingSystem.IsWindows())
        {
            return LibraryBaseName + ".dll";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "lib" + LibraryBaseName + ".dylib";
        }

        return "lib" + LibraryBaseName + ".so";
    }

    private static string GetRuntimeIdentifier()
    {
        string arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(arch))
        {
            return string.Empty;
        }

        if (OperatingSystem.IsWindows())
        {
            return "win-" + arch;
        }

        if (OperatingSystem.IsMacOS())
        {
            return "osx-" + arch;
        }

        if (OperatingSystem.IsLinux())
        {
            return "linux-" + arch;
        }

        return string.Empty;
    }

    private static string? FindRepositoryRoot(string start)
    {
        var directory = new DirectoryInfo(start);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Jellyfin.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
