using Curl.Extension.WireGuard.Noise;

namespace Curl.Extension.WireGuard.Tunnel;

/// <summary>A UDP socket connected to one peer's endpoint, closed when the tunnel through it closes.</summary>
internal interface IWireGuardUdpChannel : IWireGuardDatagramChannel, IAsyncDisposable
{
}
