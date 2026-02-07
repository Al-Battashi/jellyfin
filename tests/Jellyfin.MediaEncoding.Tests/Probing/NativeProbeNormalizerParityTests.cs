using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Jellyfin.Extensions.Json;
using Jellyfin.Extensions.Json.Converters;
using Jellyfin.NativeInterop;
using MediaBrowser.MediaEncoding.Probing;
using Xunit;

namespace Jellyfin.MediaEncoding.Tests.Probing
{
    public class NativeProbeNormalizerParityTests
    {
        [Fact]
        public void NativeNormalizer_ParityForAspectRatioNormalization()
        {
            var originalMode = Environment.GetEnvironmentVariable("JELLYFIN_NATIVE_MODE");
            try
            {
                Environment.SetEnvironmentVariable("JELLYFIN_NATIVE_MODE", "prefer");

                var runtime = new NativeInteropRuntime();
                if (!runtime.IsNativeAvailable)
                {
                    return;
                }

                var jsonOptions = new JsonSerializerOptions(JsonDefaults.Options);
                jsonOptions.Converters.Add(new JsonBoolStringConverter());

                var bytes = File.ReadAllBytes("Test Data/Probing/video_metadata.json");
                var managed = JsonSerializer.Deserialize<InternalMediaInfoResult>(bytes, jsonOptions)!;
                var expectedAspectRatios = managed.Streams
                    .Select(s => NormalizeAspect(s.DisplayAspectRatio))
                    .ToArray();
                var expectedSampleAspectRatios = managed.Streams
                    .Select(s => NormalizeAspect(s.SampleAspectRatio))
                    .ToArray();

                Assert.True(runtime.TryNormalize(bytes, out var normalizedPayload, out var error), error);
                var normalized = JsonSerializer.Deserialize<InternalMediaInfoResult>(normalizedPayload, jsonOptions)!;

                Assert.Equal(expectedAspectRatios, normalized.Streams.Select(s => s.DisplayAspectRatio).ToArray());
                Assert.Equal(expectedSampleAspectRatios, normalized.Streams.Select(s => s.SampleAspectRatio).ToArray());
            }
            finally
            {
                Environment.SetEnvironmentVariable("JELLYFIN_NATIVE_MODE", originalMode);
            }
        }

        private static string? NormalizeAspect(string? value)
        {
            return string.Equals(value, "0:1", StringComparison.OrdinalIgnoreCase) ? string.Empty : value;
        }
    }
}
