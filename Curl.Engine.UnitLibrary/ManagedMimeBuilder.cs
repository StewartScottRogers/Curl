using Curl.Api.EngineContracts.Mime;
using Curl.Api.Options.Http;

namespace Curl.Engine;

/// <summary>
/// The engine's <c>curl_mime</c> handle (ADR-0506): the parts <see cref="AddPart" /> appended, each a
/// <see cref="ManagedMimePartBuilder" />, which <see cref="Build" /> turns into the <see cref="EasyMimeBody" />
/// <c>CURLOPT_MIMEPOST</c> sends or a part nests with <see cref="IMimePartBuilder.SetSubParts" />.
/// </summary>
internal sealed class ManagedMimeBuilder : IMimeBuilder
{
    /// <summary>The parts, in the order they were added.</summary>
    private readonly List<ManagedMimePartBuilder> parts = [];

    /// <summary>
    /// Gets or sets the part whose content this body is (<c>curl_mime_subparts</c>), or <see langword="null" />
    /// while the body is no part's: libcurl's <c>curl_mime.parent</c>.
    /// </summary>
    internal ManagedMimePartBuilder? Parent { get; set; }

    /// <inheritdoc />
    public IMimePartBuilder AddPart()
    {
        ManagedMimePartBuilder part = new(this);
        parts.Add(part);
        return part;
    }

    /// <inheritdoc />
    public EasyMimeBody Build() => new() { Parts = [.. parts.Select(part => part.Build())] };
}
