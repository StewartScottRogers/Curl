using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.Options.Http;

namespace Curl.Api.Mime;

/// <summary>
/// An <see cref="IMimePartBuilder"/> that records each setter call, in order, with its argument, and
/// answers <see cref="FailingResult"/> from the setter named <see cref="FailingSetter"/>.
/// </summary>
internal sealed class FakeMimePartBuilder : IMimePartBuilder
{
    public string? FailingSetter { get; set; }

    public CurlCode FailingResult { get; set; } = CurlCode.BadFunctionArgument;

    /// <summary>The setters called, in order, by name.</summary>
    public List<string> Calls { get; } = [];

    public string? Name { get; private set; }

    public string? FileName { get; private set; }

    public string? Type { get; private set; }

    public string? Encoder { get; private set; }

    public byte[]? Data { get; private set; }

    public string? FilePath { get; private set; }

    public long CallbackSize { get; private set; }

    public ReadCallback? Read { get; private set; }

    public SeekCallback? Seek { get; private set; }

    public IMimeBuilder? SubParts { get; private set; }

    public IReadOnlyList<string>? Headers { get; private set; }

    public CurlCode SetName(string? name) => Record(nameof(SetName), () => Name = name);

    public CurlCode SetFileName(string? fileName) => Record(nameof(SetFileName), () => FileName = fileName);

    public CurlCode SetType(string? type) => Record(nameof(SetType), () => Type = type);

    public CurlCode SetEncoder(string? encoding) => Record(nameof(SetEncoder), () => Encoder = encoding);

    public CurlCode SetData(byte[]? data) => Record(nameof(SetData), () => Data = data);

    public CurlCode SetFileData(string? path) => Record(nameof(SetFileData), () => FilePath = path);

    public CurlCode SetDataCallback(long size, ReadCallback? read, SeekCallback? seek) =>
        Record(nameof(SetDataCallback), () => (CallbackSize, Read, Seek) = (size, read, seek));

    public CurlCode SetSubParts(IMimeBuilder? subParts) => Record(nameof(SetSubParts), () => SubParts = subParts);

    public CurlCode SetHeaders(IReadOnlyList<string>? headers) => Record(nameof(SetHeaders), () => Headers = headers);

    public EasyMimePart Build() => new() { Name = Name };

    private CurlCode Record(string setter, Action set)
    {
        Calls.Add(setter);
        if (setter == FailingSetter)
        {
            return FailingResult;
        }

        set();
        return CurlCode.Ok;
    }
}
