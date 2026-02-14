using System.Collections.Generic;
using Jellyfin.Api.Helpers;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Api.Tests.Helpers;

public class MediaInfoHelperSortMediaSourcesTests
{
    [Fact]
    public void SortMediaSources_WithLowBitrateCap_PrefersVersionWithinCap()
    {
        var helper = CreateMediaInfoHelper();
        var response = new PlaybackInfoResponse
        {
            MediaSources = new[]
            {
                CreateMediaSource("4k", MediaProtocol.File, true, true, 25_000_000, 3840),
                CreateMediaSource("1080p", MediaProtocol.File, true, true, 7_000_000, 1920)
            }
        };

        helper.SortMediaSources(response, 10_000_000);

        Assert.Equal("1080p", response.MediaSources[0].Id);
        Assert.Equal("4k", response.MediaSources[1].Id);
    }

    [Fact]
    public void SortMediaSources_WithHighBitrateCap_PrefersHighestQualityDirectPlay()
    {
        var helper = CreateMediaInfoHelper();
        var response = new PlaybackInfoResponse
        {
            MediaSources = new[]
            {
                CreateMediaSource("1080p", MediaProtocol.File, true, true, 7_000_000, 1920),
                CreateMediaSource("4k", MediaProtocol.File, true, true, 25_000_000, 3840)
            }
        };

        helper.SortMediaSources(response, 60_000_000);

        Assert.Equal("4k", response.MediaSources[0].Id);
        Assert.Equal("1080p", response.MediaSources[1].Id);
    }

    [Fact]
    public void SortMediaSources_WithUnknownBitrate_FallsBackToResolutionOrdering()
    {
        var helper = CreateMediaInfoHelper();
        var response = new PlaybackInfoResponse
        {
            MediaSources = new[]
            {
                CreateMediaSource("1080p", MediaProtocol.File, true, true, null, 1920),
                CreateMediaSource("4k", MediaProtocol.File, true, true, null, 3840)
            }
        };

        helper.SortMediaSources(response, 10_000_000);

        Assert.Equal("4k", response.MediaSources[0].Id);
        Assert.Equal("1080p", response.MediaSources[1].Id);
    }

    [Fact]
    public void SortMediaSources_WhenAllVersionsAreOverCap_PrefersLowestOvershoot()
    {
        var helper = CreateMediaInfoHelper();
        var response = new PlaybackInfoResponse
        {
            MediaSources = new[]
            {
                CreateMediaSource("4k", MediaProtocol.File, true, true, 14_000_000, 3840),
                CreateMediaSource("1080p", MediaProtocol.File, true, true, 10_000_000, 1920)
            }
        };

        helper.SortMediaSources(response, 8_000_000);

        Assert.Equal("1080p", response.MediaSources[0].Id);
        Assert.Equal("4k", response.MediaSources[1].Id);
    }

    [Fact]
    public void SortMediaSources_WithEquivalentSources_KeepsStableOrdering()
    {
        var helper = CreateMediaInfoHelper();
        var response = new PlaybackInfoResponse
        {
            MediaSources = new[]
            {
                CreateMediaSource("A", MediaProtocol.File, true, true, 8_000_000, 1920),
                CreateMediaSource("B", MediaProtocol.File, true, true, 8_000_000, 1920)
            }
        };

        helper.SortMediaSources(response, 10_000_000);

        Assert.Equal("A", response.MediaSources[0].Id);
        Assert.Equal("B", response.MediaSources[1].Id);
    }

    [Fact]
    public void SortMediaSources_UsesFallbackMaxBitrateWhenRequestCapMissing()
    {
        var helper = CreateMediaInfoHelper();
        var response = new PlaybackInfoResponse
        {
            MediaSources = new[]
            {
                CreateMediaSource("4k", MediaProtocol.File, true, true, 25_000_000, 3840, fallbackMaxBitrate: 10_000_000),
                CreateMediaSource("1080p", MediaProtocol.File, true, true, 7_000_000, 1920, fallbackMaxBitrate: 10_000_000)
            }
        };

        helper.SortMediaSources(response, null);

        Assert.Equal("1080p", response.MediaSources[0].Id);
        Assert.Equal("4k", response.MediaSources[1].Id);
    }

    [Fact]
    public void SortMediaSources_PrefersLocalDirectPlayOverRemoteDirectPlay()
    {
        var helper = CreateMediaInfoHelper();
        var response = new PlaybackInfoResponse
        {
            MediaSources = new[]
            {
                CreateMediaSource("remote-direct-play", MediaProtocol.Http, true, true, 8_000_000, 1920),
                CreateMediaSource("local-direct-play", MediaProtocol.File, true, true, 8_000_000, 1920)
            }
        };

        helper.SortMediaSources(response, 20_000_000);

        Assert.Equal("local-direct-play", response.MediaSources[0].Id);
        Assert.Equal("remote-direct-play", response.MediaSources[1].Id);
    }

    [Fact]
    public void SortMediaSources_PrefersDirectStreamOverTranscodeCandidates()
    {
        var helper = CreateMediaInfoHelper();
        var response = new PlaybackInfoResponse
        {
            MediaSources = new[]
            {
                CreateMediaSource("transcode-only", MediaProtocol.File, false, false, 6_000_000, 1920),
                CreateMediaSource("direct-stream", MediaProtocol.Http, false, true, 6_000_000, 1920)
            }
        };

        helper.SortMediaSources(response, 20_000_000);

        Assert.Equal("direct-stream", response.MediaSources[0].Id);
        Assert.Equal("transcode-only", response.MediaSources[1].Id);
    }

    private static MediaInfoHelper CreateMediaInfoHelper()
        => new(
            Mock.Of<IUserManager>(),
            Mock.Of<ILibraryManager>(),
            Mock.Of<IMediaSourceManager>(),
            Mock.Of<IMediaEncoder>(),
            Mock.Of<IServerConfigurationManager>(),
            Mock.Of<ILogger<MediaInfoHelper>>(),
            Mock.Of<INetworkManager>(),
            Mock.Of<IDeviceManager>());

    private static MediaSourceInfo CreateMediaSource(
        string id,
        MediaProtocol protocol,
        bool supportsDirectPlay,
        bool supportsDirectStream,
        int? bitrate,
        int width,
        int? fallbackMaxBitrate = null)
    {
        return new MediaSourceInfo
        {
            Id = id,
            Protocol = protocol,
            SupportsDirectPlay = supportsDirectPlay,
            SupportsDirectStream = supportsDirectStream,
            SupportsTranscoding = true,
            Bitrate = bitrate,
            FallbackMaxStreamingBitrate = fallbackMaxBitrate,
            MediaStreams = new List<MediaStream>
            {
                new()
                {
                    Type = MediaStreamType.Video,
                    Width = width
                }
            }
        };
    }
}
