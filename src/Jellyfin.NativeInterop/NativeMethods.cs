using System;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace Jellyfin.NativeInterop;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct NativeBuffer
{
    public readonly nint Pointer;

    public readonly nuint Length;
}

internal sealed class NativeKeyframeParseResultDto
{
    [JsonPropertyName("total_duration_ticks")]
    public long TotalDurationTicks { get; init; }

    [JsonPropertyName("keyframe_ticks")]
    public long[] KeyframeTicks { get; init; } = [];
}

internal static partial class NativeMethods
{
    private const string LibraryName = "jf_native_abi";

    [LibraryImport(LibraryName, EntryPoint = "jf_native_healthcheck")]
    internal static partial int Healthcheck();

    [LibraryImport(LibraryName, EntryPoint = "jf_native_free_buffer")]
    internal static partial void FreeBuffer(nint ptr, nuint len);

    [LibraryImport(LibraryName, EntryPoint = "jf_native_normalize_ffprobe_json")]
    internal static unsafe partial int NormalizeFfprobeJson(byte* inputPtr, nuint inputLen, out NativeBuffer output, out NativeBuffer error);

    [LibraryImport(LibraryName, EntryPoint = "jf_native_parse_keyframe_csv")]
    internal static unsafe partial int ParseKeyframeCsv(byte* inputPtr, nuint inputLen, out NativeBuffer output, out NativeBuffer error);
}
