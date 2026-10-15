namespace Curl.Api.Callbacks;

/// <summary>
/// <c>CURLOPT_READFUNCTION</c>: fills the buffer with data to upload. Upstream's <c>char *</c>
/// plus <c>size * nitems</c> is the span; <c>CURLOPT_READDATA</c> is the delegate's closure.
/// </summary>
/// <param name="buffer">The buffer to fill, valid only during the call.</param>
/// <returns>
/// The number of bytes written into <paramref name="buffer"/> (0 ends the upload), or
/// <see cref="ReadCallbackResult.Abort"/> or <see cref="ReadCallbackResult.Pause"/>.
/// </returns>
public delegate long ReadCallback(Span<byte> buffer);
