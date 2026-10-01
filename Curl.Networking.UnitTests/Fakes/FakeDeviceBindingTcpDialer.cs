using System.Net;

using Curl.Protocol.Abstractions;

namespace Curl.Networking.Fakes;

/// <summary>
/// A <see cref="FakeTcpDialer" /> whose device bind answers <see cref="DeviceBinds" />, as
/// <see cref="TcpDialer.DialFromDeviceAsync" /> does: a device bound with no address after it dials
/// unbound, anything else binds the local end chosen through <see cref="FakeTcpDialer.DialFromAsync" />.
/// </summary>
/// <param name="inner">Records the dials and answers them.</param>
/// <param name="deviceBinds">Whether <c>SO_BINDTODEVICE</c> succeeds.</param>
public sealed class FakeDeviceBindingTcpDialer(FakeTcpDialer inner, bool deviceBinds) : ITcpDialer
{
    /// <summary>Gets whether <c>SO_BINDTODEVICE</c> succeeds.</summary>
    public bool DeviceBinds { get; } = deviceBinds;

    /// <summary>Gets each device dial, in order: the device asked for and whether its address is bound after it.</summary>
    public List<(string DeviceName, bool BindsAddressAfterDevice)> DeviceDials { get; } = [];

    /// <inheritdoc />
    public ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken) =>
        inner.DialAsync(endPoint, cancellationToken);

    /// <inheritdoc />
    public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken) =>
        inner.DialFromAsync(endPoint, localEndPoint, localPortCount, cancellationToken);

    /// <inheritdoc />
    public async ValueTask<DialedTcpConnection> DialFromDeviceAsync(
        IPEndPoint endPoint,
        string deviceName,
        bool bindsAddressAfterDevice,
        Func<CancellationToken, ValueTask<IPEndPoint>> chooseLocalEndAsync,
        int localPortCount,
        CancellationToken cancellationToken)
    {
        DeviceDials.Add((deviceName, bindsAddressAfterDevice));
        if (DeviceBinds && !bindsAddressAfterDevice)
        {
            return await inner.DialAsync(endPoint, cancellationToken);
        }

        var localEndPoint = await chooseLocalEndAsync(cancellationToken);
        return await inner.DialFromAsync(endPoint, localEndPoint, localPortCount, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken) =>
        inner.DialUnixSocketAsync(address, cancellationToken);
}
