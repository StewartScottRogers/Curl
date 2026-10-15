namespace Curl.Api.Callbacks;

/// <summary>
/// <c>CURLOPT_WRITEFUNCTION</c>: takes received body data. Upstream's <c>char *</c> plus
/// <c>size * nmemb</c> is the span; <c>CURLOPT_WRITEDATA</c> is the delegate's closure.
/// </summary>
/// <param name="data">The bytes received, valid only during the call.</param>
/// <returns>
/// The number of bytes taken (anything but <paramref name="data"/>'s length fails the transfer
/// with <c>CURLE_WRITE_ERROR</c>), or <see cref="WriteCallbackResult.Pause"/> or <see cref="WriteCallbackResult.Error"/>.
/// </returns>
public delegate long WriteCallback(ReadOnlySpan<byte> data);
