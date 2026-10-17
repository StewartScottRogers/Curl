using Curl.Api.EngineContracts;
using Curl.Api.Options;

namespace Curl.Api;

/// <summary>
/// <see cref="CurlContent"/> read from a stream: each send uploads it from the stream's position at
/// that moment through a new <see cref="StreamUpload"/>.
/// </summary>
/// <param name="stream">The readable stream.</param>
/// <param name="length">The bytes to send, or <see langword="null"/> to work it out when sent.</param>
internal sealed class StreamContent(Stream stream, long? length) : CurlContent
{
    internal override EasyOptions ApplyTo(EasyOptions options, ICurlEngine engine, ICollection<IDisposable> owned)
    {
        (StreamUpload upload, long size) = StreamUpload.FromPosition(stream, length);
        return options with
        {
            Http = options.Http with { PostFields = null, PostFieldSize = size },
            Callbacks = options.Callbacks with { Read = upload.Read, Seek = upload.Seek },
        };
    }
}
