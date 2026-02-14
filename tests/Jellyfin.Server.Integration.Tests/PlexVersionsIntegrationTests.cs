using System;
using System.IO;
using System.Linq;
using Emby.Naming.Common;
using Emby.Server.Implementations.Library.Resolvers.Movies;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Server.Integration.Tests;

public sealed class PlexVersionsIntegrationTests : IClassFixture<JellyfinApplicationFactory>, IDisposable
{
    private readonly JellyfinApplicationFactory _factory;
    private readonly string _testRoot;

    public PlexVersionsIntegrationTests(JellyfinApplicationFactory factory)
    {
        _factory = factory;
        _testRoot = Path.Combine(Path.GetTempPath(), "jf-plex-versions-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
    }

    [Fact]
    public void ResolveMovieFolder_WithPlexVersions_ExposesAlternatesWithoutDuplicateMerge()
    {
        var services = _factory.Services;
        var movieResolver = new MovieResolver(
            services.GetRequiredService<IImageProcessor>(),
            NullLogger<MovieResolver>.Instance,
            new NamingOptions(),
            services.GetRequiredService<IDirectoryService>());

        var libraryPath = Path.Combine(_testRoot, "movies");
        var movieFolder = Path.Combine(libraryPath, "Black Panther (2018)");
        var plexFolder = Path.Combine(movieFolder, "Plex Versions");

        Directory.CreateDirectory(plexFolder);
        File.WriteAllText(Path.Combine(movieFolder, "Black Panther (2018).mkv"), string.Empty);
        File.WriteAllText(Path.Combine(plexFolder, "Black Panther (2018) - 1080p.mkv"), string.Empty);
        File.WriteAllText(Path.Combine(plexFolder, "Another Movie (2020).mkv"), string.Empty);

        var itemResolveArgs = new ItemResolveArgs(
            services.GetRequiredService<IServerApplicationPaths>(),
            services.GetRequiredService<ILibraryManager>())
        {
            Parent = new Folder { Path = libraryPath },
            CollectionType = CollectionType.movies,
            FileInfo = CreateDirectory(movieFolder),
            FileSystemChildren = Directory.GetFileSystemEntries(movieFolder).Select(CreateFileSystemMetadata).ToArray()
        };

        var resolved = movieResolver.ResolvePath(itemResolveArgs);

        var movie = Assert.IsType<Movie>(resolved);
        Assert.Contains(Path.Combine(plexFolder, "Black Panther (2018) - 1080p.mkv"), movie.LocalAlternateVersions, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(Path.Combine(plexFolder, "Another Movie (2020).mkv"), movie.LocalAlternateVersions, StringComparer.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, true);
        }
    }

    private static FileSystemMetadata CreateDirectory(string path)
        => new()
        {
            FullName = path,
            Name = Path.GetFileName(path),
            IsDirectory = true
        };

    private static FileSystemMetadata CreateFileSystemMetadata(string path)
        => new()
        {
            FullName = path,
            Name = Path.GetFileName(path),
            IsDirectory = Directory.Exists(path)
        };
}
