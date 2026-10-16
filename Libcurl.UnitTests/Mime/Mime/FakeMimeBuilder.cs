using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.Options.Http;

namespace Libcurl.UnitTests.Mime.Mime;

/// <summary>An <see cref="IMimeBuilder"/> that keeps its parts and builds a body naming each part.</summary>
internal sealed class FakeMimeBuilder : IMimeBuilder
{
    public List<FakeMimePartBuilder> Parts { get; } = [];

    public int BuildCount { get; private set; }

    public IMimePartBuilder AddPart()
    {
        var part = new FakeMimePartBuilder();
        Parts.Add(part);
        return part;
    }

    public EasyMimeBody Build()
    {
        BuildCount++;
        return new EasyMimeBody { Parts = [.. Parts.Select(p => new EasyMimePart { Name = p.Name })] };
    }
}

/// <summary>An <see cref="IMimePartBuilder"/> that records each call and answers <see cref="Result"/>.</summary>
internal sealed class FakeMimePartBuilder : IMimePartBuilder
{
    public CurlCode Result { get; set; }

    public List<string> Calls { get; } = [];

    public string? Name { get; private set; }

    public string? FileName { get; private set; }

    public string? Type { get; private set; }

    public string? Encoder { get; private set; }

    public byte[]? Data { get; private set; }

    public string? FilePath { get; private set; }

    public (long Size, ReadCallback? Read, SeekCallback? Seek) Callback { get; private set; }

    public IMimeBuilder? SubParts { get; private set; }

    public IReadOnlyList<string>? Headers { get; private set; }

    public CurlCode SetName(string? name) => Record(nameof(SetName), () => Name = name);

    public CurlCode SetFileName(string? fileName) => Record(nameof(SetFileName), () => FileName = fileName);

    public CurlCode SetType(string? type) => Record(nameof(SetType), () => Type = type);

    public CurlCode SetEncoder(string? encoding) => Record(nameof(SetEncoder), () => Encoder = encoding);

    public CurlCode SetData(byte[]? data) => Record(nameof(SetData), () => Data = data);

    public CurlCode SetFileData(string? path) => Record(nameof(SetFileData), () => FilePath = path);

    public CurlCode SetDataCallback(long size, ReadCallback? read, SeekCallback? seek) => Record(nameof(SetDataCallback), () => Callback = (size, read, seek));

    public CurlCode SetSubParts(IMimeBuilder? subParts) => Record(nameof(SetSubParts), () => SubParts = subParts);

    public CurlCode SetHeaders(IReadOnlyList<string>? headers) => Record(nameof(SetHeaders), () => Headers = headers);

    private CurlCode Record(string call, Action keep)
    {
        Calls.Add(call);
        keep();
        return Result;
    }
}
