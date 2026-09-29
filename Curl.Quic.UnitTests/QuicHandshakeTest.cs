namespace Curl.Quic;

/// <summary>Shared set-up for the handshake tests.</summary>
internal static class QuicHandshakeTest
{
    /// <summary>The settings of curl's build for <c>localhost</c>.</summary>
    public static QuicClientSettings CurlSettings { get; } = new() { Tls = QuicClientSettings.CreateCurlTlsSettings("localhost") };

    /// <summary>A client with curl's settings, fixed randomness and a verifier that accepts every chain.</summary>
    public static QuicClientHandshake Client(QuicClientSettings? settings = null, QuicTestVerifier? verifier = null) =>
        new(settings ?? CurlSettings, new QuicTestRandomSource(), verifier ?? new QuicTestVerifier());
}
