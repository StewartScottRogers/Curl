namespace Curl.Api.Callbacks;

/// <summary>
/// <c>CURLOPT_INTERLEAVEFUNCTION</c>: takes RTP data interleaved in an RTSP stream, each call one
/// whole packet with its <c>$</c> header. <c>CURLOPT_INTERLEAVEDATA</c> is the delegate's closure.
/// </summary>
/// <param name="data">The packet's bytes, valid only during the call.</param>
/// <returns>The number of bytes taken; anything but <paramref name="data"/>'s length fails the transfer.</returns>
public delegate long InterleaveCallback(ReadOnlySpan<byte> data);
