namespace Curl.Api.Callbacks;

/// <summary>
/// <c>CURLOPT_HEADERFUNCTION</c>: takes one received header line, end of line included.
/// <c>CURLOPT_HEADERDATA</c> is the delegate's closure.
/// </summary>
/// <param name="header">The header line's bytes, valid only during the call.</param>
/// <returns>
/// The number of bytes taken (anything but <paramref name="header"/>'s length fails the transfer
/// with <c>CURLE_WRITE_ERROR</c>), or <see cref="WriteCallbackResult.Error"/>.
/// </returns>
public delegate long HeaderCallback(ReadOnlySpan<byte> header);
