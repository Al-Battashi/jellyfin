using System;
using System.Collections.Generic;
using System.IO;
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
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.Library;

public class MovieResolverTests
{
    private static readonly NamingOptions _namingOptions = new();

    [Fact]
    public void Resolve_GivenLocalAlternateVersion_ResolvesToVideo()
    {
        var movieResolver = new MovieResolver(Mock.Of<IImageProcessor>(), Mock.Of<ILogger<MovieResolver>>(), _namingOptions, Mock.Of<IDirectoryService>());
        var itemResolveArgs = new ItemResolveArgs(
            Mock.Of<IServerApplicationPaths>(),
            null)
        {
            Parent = null,
            FileInfo = new FileSystemMetadata
            {
                FullName = "/movies/Black Panther (2018)/Black Panther (2018) - 1080p 3D.mk3d"
            }
        };

        Assert.NotNull(movieResolver.Resolve(itemResolveArgs));
    }

    [Fact]
    public void Resolve_GivenPlexVersionsFolder_ResolvesAsSingleMovieWithAlternates()
    {
        const string movieName = "Black Panther (2018)";
        const string plexFolderName = "Plex Versions";
        var movieFolderPath = GetMovieFolderPath(movieName);
        var plexFolderPath = Path.Combine(movieFolderPath, plexFolderName);
        var directoryService = BuildDirectoryServiceMock(plexFolderPath, new[]
        {
            CreateFile(Path.Combine(plexFolderPath, "Black Panther (2018) - 1080p.mkv")),
            CreateFile(Path.Combine(plexFolderPath, "Black Panther (2018) - 720p.mkv"))
        });

        var movieResolver = CreateMovieResolver(directoryService.Object);
        var itemResolveArgs = BuildMoviesCollectionResolveArgs(movieName, new[]
        {
            CreateDirectory(plexFolderPath),
            CreateFile(Path.Combine(movieFolderPath, "Black Panther (2018).mkv"))
        });

        var result = movieResolver.Resolve(itemResolveArgs);

        var movie = Assert.IsType<Movie>(result);
        Assert.Equal(2, movie.LocalAlternateVersions.Length);
        Assert.Contains(Path.Combine(plexFolderPath, "Black Panther (2018) - 1080p.mkv"), movie.LocalAlternateVersions, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Path.Combine(plexFolderPath, "Black Panther (2018) - 720p.mkv"), movie.LocalAlternateVersions, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_GivenCaseInsensitivePlexVersionsFolder_ResolvesAlternates()
    {
        const string movieName = "Dune (2021)";
        const string plexFolderName = "pLeX vErSiOnS";
        var movieFolderPath = GetMovieFolderPath(movieName);
        var plexFolderPath = Path.Combine(movieFolderPath, plexFolderName);
        var directoryService = BuildDirectoryServiceMock(plexFolderPath, new[]
        {
            CreateFile(Path.Combine(plexFolderPath, "Dune (2021) - 1080p.mkv"))
        });

        var movieResolver = CreateMovieResolver(directoryService.Object);
        var itemResolveArgs = BuildMoviesCollectionResolveArgs(movieName, new[]
        {
            CreateDirectory(plexFolderPath),
            CreateFile(Path.Combine(movieFolderPath, "Dune (2021).mkv"))
        });

        var result = movieResolver.Resolve(itemResolveArgs);

        var movie = Assert.IsType<Movie>(result);
        Assert.Single(movie.LocalAlternateVersions);
        Assert.Contains(Path.Combine(plexFolderPath, "Dune (2021) - 1080p.mkv"), movie.LocalAlternateVersions, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_GivenNonVideoFilesInPlexVersions_IgnoresNonVideoFiles()
    {
        const string movieName = "Avatar (2009)";
        const string plexFolderName = "Plex Versions";
        var movieFolderPath = GetMovieFolderPath(movieName);
        var plexFolderPath = Path.Combine(movieFolderPath, plexFolderName);
        var directoryService = BuildDirectoryServiceMock(plexFolderPath, new[]
        {
            CreateFile(Path.Combine(plexFolderPath, "Avatar (2009) - 1080p.mkv")),
            CreateFile(Path.Combine(plexFolderPath, "Readme.txt"))
        });

        var movieResolver = CreateMovieResolver(directoryService.Object);
        var itemResolveArgs = BuildMoviesCollectionResolveArgs(movieName, new[]
        {
            CreateDirectory(plexFolderPath),
            CreateFile(Path.Combine(movieFolderPath, "Avatar (2009).mkv"))
        });

        var result = movieResolver.Resolve(itemResolveArgs);

        var movie = Assert.IsType<Movie>(result);
        Assert.Single(movie.LocalAlternateVersions);
        Assert.Contains(Path.Combine(plexFolderPath, "Avatar (2009) - 1080p.mkv"), movie.LocalAlternateVersions, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(Path.Combine(plexFolderPath, "Readme.txt"), movie.LocalAlternateVersions, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_GivenNestedPlexVersionsFolders_ResolvesAlternatesByMediaIdentity()
    {
        const string movieName = "Baby Driver (2017)";
        var movieFolderPath = GetMovieFolderPath(movieName);
        var plexFolderPath = Path.Combine(movieFolderPath, "Plex Versions");
        var lowFolderPath = Path.Combine(plexFolderPath, "1080p low");
        var sdrFolderPath = Path.Combine(plexFolderPath, "4k sdr");
        var directoryService = BuildDirectoryServiceMock(
            new Dictionary<string, FileSystemMetadata[]>
            {
                [plexFolderPath] =
                [
                    CreateDirectory(lowFolderPath),
                    CreateDirectory(sdrFolderPath)
                ],
                [lowFolderPath] =
                [
                    CreateFile(Path.Combine(lowFolderPath, "Baby Driver (2017).mkv"))
                ],
                [sdrFolderPath] =
                [
                    CreateFile(Path.Combine(sdrFolderPath, "Baby Driver (2017).mp4"))
                ]
            });

        var movieResolver = CreateMovieResolver(directoryService.Object);
        var itemResolveArgs = BuildMoviesCollectionResolveArgs(movieName, new[]
        {
            CreateDirectory(plexFolderPath),
            CreateFile(Path.Combine(movieFolderPath, "Baby Driver (2017) [2160p].mp4"))
        });

        var result = movieResolver.Resolve(itemResolveArgs);

        var movie = Assert.IsType<Movie>(result);
        Assert.Contains(Path.Combine(lowFolderPath, "Baby Driver (2017).mkv"), movie.LocalAlternateVersions, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Path.Combine(sdrFolderPath, "Baby Driver (2017).mp4"), movie.LocalAlternateVersions, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_GivenPlexVersionWithSameBaseNameWithoutTags_StillResolvesAsAlternate()
    {
        const string movieName = "Baby Driver (2017)";
        var movieFolderPath = GetMovieFolderPath(movieName);
        var plexFolderPath = Path.Combine(movieFolderPath, "Plex Versions");
        var directoryService = BuildDirectoryServiceMock(
            new Dictionary<string, FileSystemMetadata[]>
            {
                [plexFolderPath] =
                [
                    CreateFile(Path.Combine(plexFolderPath, "Baby Driver (2017).mkv"))
                ]
            });

        var movieResolver = CreateMovieResolver(directoryService.Object);
        var itemResolveArgs = BuildMoviesCollectionResolveArgs(movieName, new[]
        {
            CreateDirectory(plexFolderPath),
            CreateFile(Path.Combine(movieFolderPath, "Baby Driver (2017) [2160p].mp4"))
        });

        var result = movieResolver.Resolve(itemResolveArgs);

        var movie = Assert.IsType<Movie>(result);
        Assert.Contains(Path.Combine(plexFolderPath, "Baby Driver (2017).mkv"), movie.LocalAlternateVersions, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_GivenNonMatchingPlexVersionFile_DoesNotMergeAsAlternate()
    {
        const string movieName = "Interstellar (2014)";
        const string plexFolderName = "Plex Versions";
        var movieFolderPath = GetMovieFolderPath(movieName);
        var plexFolderPath = Path.Combine(movieFolderPath, plexFolderName);
        var directoryService = BuildDirectoryServiceMock(plexFolderPath, new[]
        {
            CreateFile(Path.Combine(plexFolderPath, "Another Movie (2020).mkv"))
        });

        var movieResolver = CreateMovieResolver(directoryService.Object);
        var itemResolveArgs = BuildMoviesCollectionResolveArgs(movieName, new[]
        {
            CreateDirectory(plexFolderPath),
            CreateFile(Path.Combine(movieFolderPath, "Interstellar (2014).mkv"))
        });

        var result = movieResolver.Resolve(itemResolveArgs);

        var movie = Assert.IsType<Movie>(result);
        Assert.Empty(movie.LocalAlternateVersions);
    }

    [Fact]
    public void Resolve_GivenYearMismatchInPlexVersions_DoesNotMergeAsAlternate()
    {
        const string movieName = "Baby Driver (2017)";
        var movieFolderPath = GetMovieFolderPath(movieName);
        var plexFolderPath = Path.Combine(movieFolderPath, "Plex Versions");
        var directoryService = BuildDirectoryServiceMock(
            new Dictionary<string, FileSystemMetadata[]>
            {
                [plexFolderPath] =
                [
                    CreateFile(Path.Combine(plexFolderPath, "Baby Driver (2018).mkv"))
                ]
            });

        var movieResolver = CreateMovieResolver(directoryService.Object);
        var itemResolveArgs = BuildMoviesCollectionResolveArgs(movieName, new[]
        {
            CreateDirectory(plexFolderPath),
            CreateFile(Path.Combine(movieFolderPath, "Baby Driver (2017) [2160p].mp4"))
        });

        var result = movieResolver.Resolve(itemResolveArgs);

        var movie = Assert.IsType<Movie>(result);
        Assert.Empty(movie.LocalAlternateVersions);
    }

    [Fact]
    public void Resolve_GivenMultipleMovieFilesInSameFolder_KeepsMovieGroupingCorrect()
    {
        const string movieName = "Baby Driver (2017)";
        var movieFolderPath = GetMovieFolderPath(movieName);
        var plexFolderPath = Path.Combine(movieFolderPath, "Plex Versions");
        var directoryService = BuildDirectoryServiceMock(
            new Dictionary<string, FileSystemMetadata[]>
            {
                [plexFolderPath] =
                [
                    CreateFile(Path.Combine(plexFolderPath, "Baby Driver (2017).mkv"))
                ]
            });

        var movieResolver = CreateMovieResolver(directoryService.Object);
        var itemResolveArgs = BuildMoviesCollectionResolveArgs(movieName, new[]
        {
            CreateDirectory(plexFolderPath),
            CreateFile(Path.Combine(movieFolderPath, "Baby Driver (2017) [2160p].mp4")),
            CreateFile(Path.Combine(movieFolderPath, "Baby Driver (2017) [1080p].mkv")),
            CreateFile(Path.Combine(movieFolderPath, "Another Movie (2020).mp4"))
        });

        var result = movieResolver.Resolve(itemResolveArgs);

        // With mixed unrelated movie files present, folder should not be reduced to one movie.
        Assert.Null(result);
    }

    [Fact]
    public void Resolve_GivenUnknownSubfolder_BehaviorIsUnchangedAndDoesNotResolveSingleMovie()
    {
        const string movieName = "Blade Runner 2049 (2017)";
        var movieFolderPath = GetMovieFolderPath(movieName);
        var unknownSubfolderPath = Path.Combine(movieFolderPath, "Subtitles");
        var directoryService = BuildDirectoryServiceMock(unknownSubfolderPath, Array.Empty<FileSystemMetadata>());

        var movieResolver = CreateMovieResolver(directoryService.Object);
        var itemResolveArgs = BuildMoviesCollectionResolveArgs(movieName, new[]
        {
            CreateDirectory(unknownSubfolderPath),
            CreateFile(Path.Combine(movieFolderPath, "Blade Runner 2049 (2017).mkv"))
        });

        var result = movieResolver.Resolve(itemResolveArgs);

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_GivenExtrasSubfolder_BehaviorIsUnchangedAndStillResolvesMovie()
    {
        const string movieName = "The Matrix (1999)";
        var movieFolderPath = GetMovieFolderPath(movieName);
        var extrasFolderPath = Path.Combine(movieFolderPath, "extras");
        var directoryService = BuildDirectoryServiceMock(extrasFolderPath, Array.Empty<FileSystemMetadata>());

        var movieResolver = CreateMovieResolver(directoryService.Object);
        var itemResolveArgs = BuildMoviesCollectionResolveArgs(movieName, new[]
        {
            CreateDirectory(extrasFolderPath),
            CreateFile(Path.Combine(movieFolderPath, "The Matrix (1999).mkv"))
        });

        var result = movieResolver.Resolve(itemResolveArgs);

        Assert.IsType<Movie>(result);
    }

    private static MovieResolver CreateMovieResolver(IDirectoryService directoryService)
        => new(Mock.Of<IImageProcessor>(), Mock.Of<ILogger<MovieResolver>>(), _namingOptions, directoryService);

    private static Mock<IDirectoryService> BuildDirectoryServiceMock(string folderPath, FileSystemMetadata[] folderChildren)
        => BuildDirectoryServiceMock(new Dictionary<string, FileSystemMetadata[]>
        {
            [folderPath] = folderChildren
        });

    private static Mock<IDirectoryService> BuildDirectoryServiceMock(Dictionary<string, FileSystemMetadata[]> childrenByFolder)
    {
        var directoryService = new Mock<IDirectoryService>();
        directoryService.Setup(i => i.GetFilePaths(It.IsAny<string>())).Returns(Array.Empty<string>());
        directoryService.Setup(i => i.GetFileSystemEntries(It.IsAny<string>()))
            .Returns((string path) =>
            {
                foreach (var (folder, children) in childrenByFolder)
                {
                    if (string.Equals(path, folder, StringComparison.OrdinalIgnoreCase))
                    {
                        return children;
                    }
                }

                return Array.Empty<FileSystemMetadata>();
            });
        return directoryService;
    }

    private static ItemResolveArgs BuildMoviesCollectionResolveArgs(string movieName, FileSystemMetadata[] children)
    {
        var movieFolderPath = GetMovieFolderPath(movieName);
        var parent = new Folder { Path = "/movies" };

        return new ItemResolveArgs(Mock.Of<IServerApplicationPaths>(), Mock.Of<ILibraryManager>())
        {
            Parent = parent,
            CollectionType = CollectionType.movies,
            FileInfo = CreateDirectory(movieFolderPath),
            FileSystemChildren = children
        };
    }

    private static string GetMovieFolderPath(string movieName)
        => Path.Combine("/movies", movieName);

    private static FileSystemMetadata CreateDirectory(string path)
        => new()
        {
            FullName = path,
            Name = Path.GetFileName(path),
            IsDirectory = true
        };

    private static FileSystemMetadata CreateFile(string path)
        => new()
        {
            FullName = path,
            Name = Path.GetFileName(path),
            IsDirectory = false
        };
}
