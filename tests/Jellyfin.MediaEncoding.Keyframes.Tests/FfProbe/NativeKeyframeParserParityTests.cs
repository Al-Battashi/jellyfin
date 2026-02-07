using System;
using System.IO;
using System.Text.Json;
using Jellyfin.NativeInterop;
using Xunit;

namespace Jellyfin.MediaEncoding.Keyframes.FfProbe
{
    public class NativeKeyframeParserParityTests
    {
        [Theory]
        [InlineData("keyframes.txt", "keyframes_result.json")]
        [InlineData("keyframes_streamduration.txt", "keyframes_streamduration_result.json")]
        public void NativeParser_ParityWithExpectedFixtures(string testDataFileName, string resultFileName)
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

                var testDataPath = Path.Combine("FfProbe/Test Data", testDataFileName);
                var resultPath = Path.Combine("FfProbe/Test Data", resultFileName);
                var input = File.ReadAllText(testDataPath);

                using var resultFileStream = File.OpenRead(resultPath);
                var expectedResult = JsonSerializer.Deserialize<KeyframeData>(resultFileStream)!;

                Assert.True(runtime.TryParse(input, out var nativeResult, out var error), error);
                Assert.Equal(expectedResult.TotalDuration, nativeResult.TotalDurationTicks);
                Assert.Equal(expectedResult.KeyframeTicks, nativeResult.KeyframeTicks);
            }
            finally
            {
                Environment.SetEnvironmentVariable("JELLYFIN_NATIVE_MODE", originalMode);
            }
        }
    }
}
