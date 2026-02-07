using System;
using System.Collections.Generic;

namespace Jellyfin.NativeInterop;

public interface INativeInteropRuntime
{
    NativeInteropMode Mode { get; }

    bool IsNativeAvailable { get; }

    void EnsureRuntimeAvailability();
}

public interface INativeProbeNormalizer
{
    NativeInteropMode Mode { get; }

    bool IsNativeAvailable { get; }

    bool TryNormalize(ReadOnlyMemory<byte> ffprobeJson, out byte[] normalizedJson, out string? error);
}

public interface INativeKeyframeParser
{
    NativeInteropMode Mode { get; }

    bool IsNativeAvailable { get; }

    bool TryParse(string input, out NativeKeyframeParseResult result, out string? error);
}

public readonly record struct NativeKeyframeParseResult(long TotalDurationTicks, IReadOnlyList<long> KeyframeTicks);
