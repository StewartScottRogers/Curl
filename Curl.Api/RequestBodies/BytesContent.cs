using Curl.Api.Options;

namespace Curl.Api;

/// <summary>In-memory <see cref="CurlContent"/>: <c>CURLOPT_POSTFIELDS</c> with its size.</summary>
/// <param name="bytes">The body.</param>
internal sealed class BytesContent(ReadOnlyMemory<byte> bytes) : CurlContent
{
    /// <summary>The body.</summary>
    public ReadOnlyMemory<byte> Bytes => bytes;

    internal override EasyOptions ApplyTo(EasyOptions options) =>
        options with { Http = options.Http with { PostFields = bytes, PostFieldSize = bytes.Length } };
}
