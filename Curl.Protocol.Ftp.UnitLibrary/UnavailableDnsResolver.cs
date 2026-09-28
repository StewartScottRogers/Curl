using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp;

/// <summary>
/// The <see cref="IDnsResolver" /> of an <see cref="FtpProtocolHandler" /> built without
/// one: it resolves nothing, so a host name given to <c>-P</c> ends with exit 6 and
/// <c>Could not resolve host: &lt;name&gt;</c>, as curl 8.21.0 ends for a name that does not
/// resolve (ADR-0108).
/// </summary>
internal sealed class UnavailableDnsResolver : IDnsResolver
{
    /// <summary>The one instance.</summary>
    public static readonly UnavailableDnsResolver Instance = new();

    private UnavailableDnsResolver()
    {
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<IPAddress>>([]);
}
