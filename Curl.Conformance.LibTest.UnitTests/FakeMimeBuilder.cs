using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.Options.Http;

namespace Curl.Conformance.LibTest.UnitTests;

/// <summary>An <see cref="IMimeBuilder"/> whose parts keep their name and data and build a body naming each part.</summary>
internal sealed class FakeMimeBuilder : IMimeBuilder
{
    public List<FakeMimePartBuilder> Parts { get; } = [];

    public IMimePartBuilder AddPart()
    {
        var part = new FakeMimePartBuilder();
        Parts.Add(part);
        return part;
    }

    public EasyMimeBody Build() => new() { Parts = [.. Parts.Select(p => new EasyMimePart { Name = p.Name })] };
}

/// <summary>An <see cref="IMimePartBuilder"/> that keeps the name and data it is given.</summary>
internal sealed class FakeMimePartBuilder : IMimePartBuilder
{
    public string? Name { get; private set; }

    public byte[]? Data { get; private set; }

    public CurlCode SetName(string? name)
    {
        Name = name;
        return CurlCode.Ok;
    }

    public CurlCode SetData(byte[]? data)
    {
        Data = data;
        return CurlCode.Ok;
    }

    public CurlCode SetFileName(string? fileName) => throw new NotSupportedException();

    public CurlCode SetType(string? type) => throw new NotSupportedException();

    public CurlCode SetEncoder(string? encoding) => throw new NotSupportedException();

    public CurlCode SetFileData(string? path) => throw new NotSupportedException();

    public CurlCode SetDataCallback(long size, ReadCallback? read, SeekCallback? seek) => throw new NotSupportedException();

    public CurlCode SetSubParts(IMimeBuilder? subParts) => throw new NotSupportedException();

    public CurlCode SetHeaders(IReadOnlyList<string>? headers) => throw new NotSupportedException();
}
