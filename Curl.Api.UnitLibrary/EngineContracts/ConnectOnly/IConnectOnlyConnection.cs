namespace Curl.Api.EngineContracts.ConnectOnly;

/// <summary>
/// The raw connection of an easy handle performed with <c>CURLOPT_CONNECT_ONLY</c> 1: libcurl's
/// <c>curl_easy_recv</c> and <c>curl_easy_send</c>, which move bytes on it with no protocol.
/// </summary>
public interface IConnectOnlyConnection
{
    /// <summary><c>curl_easy_recv</c>: receive whatever bytes the connection has ready.</summary>
    /// <param name="buffer">Where the received bytes are written.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    /// <returns>The result and the byte count; <c>CURLE_AGAIN</c> when nothing is ready, <c>CURLE_OK</c> with 0 bytes when the peer closed.</returns>
    ValueTask<CurlConnectOnlyResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken);

    /// <summary><c>curl_easy_send</c>: send as many bytes as the connection takes now.</summary>
    /// <param name="buffer">The bytes to send.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    /// <returns>The result and the byte count sent; <c>CURLE_AGAIN</c> when nothing could be sent yet.</returns>
    ValueTask<CurlConnectOnlyResult> SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken);
}
