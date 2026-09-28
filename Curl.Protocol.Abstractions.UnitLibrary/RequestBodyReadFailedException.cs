namespace Curl.Protocol.Abstractions;

/// <summary>
/// The <see cref="IOException" /> a request body's stream throws when a read fails in a way
/// curl reports as a failure, not as the end of the body: a multipart part whose encoder
/// refuses the data it reached, as <c>7bit</c> refuses a byte above 127.
/// </summary>
/// <remarks>
/// A protocol handler that sends the body stops at it and fails the transfer with exit 26,
/// <see cref="CurlExitCode.ReadError" />, and its <see cref="Exception.Message" />, as curl
/// 8.21.0's <c>lib/mime.c</c> reader returns <c>READ_ERROR</c>. Any other
/// <see cref="IOException" /> from a body stream is the end of the body, as curl takes a file
/// read that fails. Because the type derives from <see cref="IOException" />, a reader that
/// catches <see cref="IOException" /> still catches it.
/// </remarks>
/// <param name="message">The message curl gives for the failure.</param>
public sealed class RequestBodyReadFailedException(string message) : IOException(message);
