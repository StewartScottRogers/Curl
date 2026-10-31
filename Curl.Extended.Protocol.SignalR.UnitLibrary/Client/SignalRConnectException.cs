using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.SignalR.Client;

/// <summary>Curl's connector could not open a connection to the negotiate or WebSocket URL; <see cref="Result"/> says why.</summary>
/// <param name="result">The connector's failed result.</param>
internal sealed class SignalRConnectException(ConnectResult result) : Exception(result.ErrorMessage)
{
    /// <summary>Gets the connector's failed result.</summary>
    public ConnectResult Result { get; } = result;
}
