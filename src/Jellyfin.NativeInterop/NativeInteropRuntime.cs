using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Jellyfin.NativeInterop;

public sealed class NativeInteropRuntime : INativeInteropRuntime, INativeProbeNormalizer, INativeKeyframeParser
{
    private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly Lazy<bool> _nativeAvailability;

    public NativeInteropRuntime()
    {
        NativeLibraryLocator.EnsureResolverInitialized();
        _nativeAvailability = new Lazy<bool>(CheckAvailability);
    }

    public NativeInteropMode Mode { get; } = ResolveMode();

    public bool IsNativeAvailable => _nativeAvailability.Value;

    public void EnsureRuntimeAvailability()
    {
        if (Mode == NativeInteropMode.Required && !IsNativeAvailable)
        {
            throw new InvalidOperationException("JELLYFIN_NATIVE_MODE=required but jf_native_abi could not be loaded. Set JELLYFIN_NATIVE_LIBRARY_PATH or switch mode to prefer/disabled.");
        }
    }

    public bool TryNormalize(ReadOnlyMemory<byte> ffprobeJson, out byte[] normalizedJson, out string? error)
    {
        normalizedJson = [];
        error = null;

        if (Mode == NativeInteropMode.Disabled)
        {
            error = "native mode disabled";
            return false;
        }

        if (!IsNativeAvailable)
        {
            error = "native library unavailable";
            return false;
        }

        unsafe
        {
            fixed (byte* inputPtr = ffprobeJson.Span)
            {
                var exitCode = NativeMethods.NormalizeFfprobeJson(inputPtr, (nuint)ffprobeJson.Length, out var output, out var nativeError);
                return TryReadResult(exitCode, output, nativeError, out normalizedJson, out error);
            }
        }
    }

    public bool TryParse(string input, out NativeKeyframeParseResult result, out string? error)
    {
        result = default;
        error = null;

        if (Mode == NativeInteropMode.Disabled)
        {
            error = "native mode disabled";
            return false;
        }

        if (!IsNativeAvailable)
        {
            error = "native library unavailable";
            return false;
        }

        var inputBytes = Encoding.UTF8.GetBytes(input);

        unsafe
        {
            fixed (byte* inputPtr = inputBytes)
            {
                var exitCode = NativeMethods.ParseKeyframeCsv(inputPtr, (nuint)inputBytes.Length, out var output, out var nativeError);
                if (!TryReadResult(exitCode, output, nativeError, out var payload, out error))
                {
                    return false;
                }

                var dto = JsonSerializer.Deserialize<NativeKeyframeParseResultDto>(payload, _jsonSerializerOptions);
                if (dto is null)
                {
                    error = "native parser returned an invalid payload";
                    return false;
                }

                result = new NativeKeyframeParseResult(dto.TotalDurationTicks, dto.KeyframeTicks);
                return true;
            }
        }
    }

    private static bool TryReadResult(int exitCode, NativeBuffer output, NativeBuffer nativeError, out byte[] payload, out string? error)
    {
        payload = [];
        error = null;

        try
        {
            if (exitCode == 0)
            {
                payload = CopyBuffer(output);
                return true;
            }

            var errorBytes = CopyBuffer(nativeError);
            error = errorBytes.Length == 0 ? "native call failed" : Encoding.UTF8.GetString(errorBytes);
            return false;
        }
        finally
        {
            FreeBuffer(output);
            FreeBuffer(nativeError);
        }
    }

    private static byte[] CopyBuffer(NativeBuffer buffer)
    {
        if (buffer.Pointer == nint.Zero || buffer.Length == 0)
        {
            return [];
        }

        var length = checked((int)buffer.Length);
        var bytes = new byte[length];
        Marshal.Copy(buffer.Pointer, bytes, 0, length);
        return bytes;
    }

    private static void FreeBuffer(NativeBuffer buffer)
    {
        if (buffer.Pointer == nint.Zero || buffer.Length == 0)
        {
            return;
        }

        NativeMethods.FreeBuffer(buffer.Pointer, buffer.Length);
    }

    private static bool CheckAvailability()
    {
        try
        {
            return NativeMethods.Healthcheck() == 1;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            return false;
        }
    }

    private static NativeInteropMode ResolveMode()
    {
        var mode = Environment.GetEnvironmentVariable("JELLYFIN_NATIVE_MODE");

        if (string.IsNullOrWhiteSpace(mode))
        {
            return NativeInteropMode.Required;
        }

        return mode.Trim().ToLowerInvariant() switch
        {
            "required" => NativeInteropMode.Required,
            "prefer" => NativeInteropMode.Prefer,
            "disabled" => NativeInteropMode.Disabled,
            _ => NativeInteropMode.Required
        };
    }
}
