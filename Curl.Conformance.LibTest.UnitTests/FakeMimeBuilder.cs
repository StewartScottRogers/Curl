using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.Options.Http;

namespace Curl.Conformance.LibTest.UnitTests;

internal sealed class FakeMimeBuilder(CurlCode answer = CurlCode.Ok) : IMimeBuilder
{
    public List<FakeMimePartBuilder> Parts { get; } = [];

    public IMimePartBuilder AddPart()
    {
        var part = new FakeMimePartBuilder(answer);
        Parts.Add(part);
        return part;
    }

    public EasyMimeBody Build() => new() { Parts = [.. Parts.Select(p => new EasyMimePart { Name = p.Name })] };
}

internal sealed class FakeMimePartBuilder(CurlCode answer) : IMimePartBuilder
{
    public string? Name { get; private set; }

    public byte[]? Data { get; private set; }

    public List<string> Calls { get; } = [];

    public CurlCode SetName(string? name)
    {
        Name = name;
        return Answer(nameof(SetName));
    }

    public CurlCode SetData(byte[]? data)
    {
        Data = data;
        return Answer(nameof(SetData));
    }

    public CurlCode SetFileName(string? fileName) => Answer($"{nameof(SetFileName)} {fileName}");

    public CurlCode SetType(string? type) => Answer($"{nameof(SetType)} {type}");

    public CurlCode SetEncoder(string? encoding) => Answer($"{nameof(SetEncoder)} {encoding}");

    public CurlCode SetFileData(string? path) => Answer($"{nameof(SetFileData)} {path}");

    public CurlCode SetDataCallback(long size, ReadCallback? read, SeekCallback? seek) => Answer($"{nameof(SetDataCallback)} {size}");

    public CurlCode SetSubParts(IMimeBuilder? subParts) => Answer(nameof(SetSubParts));

    public CurlCode SetHeaders(IReadOnlyList<string>? headers) => Answer($"{nameof(SetHeaders)} {string.Join('|', headers ?? [])}");

    private CurlCode Answer(string call)
    {
        Calls.Add(call);
        return answer;
    }
}
