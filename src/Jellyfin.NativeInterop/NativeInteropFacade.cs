using System;

namespace Jellyfin.NativeInterop;

public static class NativeInteropFacade
{
    private static readonly Lazy<NativeInteropRuntime> _runtime = new(() => new NativeInteropRuntime());

    public static INativeInteropRuntime Runtime => _runtime.Value;

    public static INativeProbeNormalizer ProbeNormalizer => _runtime.Value;

    public static INativeKeyframeParser KeyframeParser => _runtime.Value;
}
