using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// An <see cref="ITlsProvider" /> that cannot finish some handshakes itself and asks for them to be
/// run again, over a newly dialled connection, by the hand-built TLS client (ADR-1459, BL-6066).
/// </summary>
internal interface ITlsProviderWithHandBuiltRedial : ITlsProvider
{
    /// <summary>Returns the provider that runs the next handshake with a host.</summary>
    /// <param name="targetHost">The host the handshake is with.</param>
    /// <returns>
    /// The hand-built client once a handshake with the host asked for a redial, otherwise this provider.
    /// </returns>
    ITlsProvider ProviderFor(string targetHost);

    /// <summary>Whether a failed handshake of this provider asks to be run again.</summary>
    /// <param name="handshake">The result this provider returned.</param>
    /// <returns>
    /// <see langword="true" /> when the caller, dialling again and asking <see cref="ProviderFor" />
    /// anew, can finish the handshake.
    /// </returns>
    bool AsksForRedial(ConnectResult handshake);
}
