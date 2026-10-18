namespace Curl.Api;

/// <summary>Which way a transfer's bytes are moving when a <see cref="CurlTransferProgress"/> is reported.</summary>
public enum CurlTransferPhase
{
    /// <summary>No byte of the body has moved yet: resolving, connecting, handshaking or sending the request head.</summary>
    Connecting,

    /// <summary>The request body is being sent and no response body byte has arrived yet.</summary>
    Uploading,

    /// <summary>The response body is arriving, or its size is known.</summary>
    Downloading,
}
