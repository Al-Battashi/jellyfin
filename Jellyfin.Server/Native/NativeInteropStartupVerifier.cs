using System.Threading;
using System.Threading.Tasks;
using Jellyfin.NativeInterop;
using Microsoft.Extensions.Hosting;

namespace Jellyfin.Server.Native;

internal sealed class NativeInteropStartupVerifier : IHostedService
{
    private readonly INativeInteropRuntime _nativeInteropRuntime;

    public NativeInteropStartupVerifier(INativeInteropRuntime nativeInteropRuntime)
    {
        _nativeInteropRuntime = nativeInteropRuntime;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _nativeInteropRuntime.EnsureRuntimeAvailability();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
