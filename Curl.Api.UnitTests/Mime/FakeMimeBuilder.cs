using Curl.Api.EngineContracts.Mime;
using Curl.Api.Options.Http;

namespace Curl.Api.Mime;

/// <summary>An <see cref="IMimeBuilder"/> whose parts are <see cref="FakeMimePartBuilder"/>s, each set up by <see cref="ConfigurePart"/>.</summary>
internal class FakeMimeBuilder : IMimeBuilder
{
    public Action<FakeMimePartBuilder> ConfigurePart { get; set; } = _ => { };

    public List<FakeMimePartBuilder> Parts { get; } = [];

    public IMimePartBuilder AddPart()
    {
        FakeMimePartBuilder part = new();
        ConfigurePart(part);
        Parts.Add(part);
        return part;
    }

    public EasyMimeBody Build() => new() { Parts = [.. Parts.Select(part => part.Build())] };
}
