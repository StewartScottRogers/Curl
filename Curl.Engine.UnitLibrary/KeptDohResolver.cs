using Curl.Api.Options.Network;
using Curl.Api.Options.Tls;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// The <c>--doh-url</c> resolver a library handle keeps from one perform to the next, so a later perform asks the DoH
/// server over the connections an earlier one opened, as libcurl keeps them in the handle's connection cache
/// (ADR-1449). It is kept while the options it was built from are unchanged: the TLS options, and the network options
/// but for the URL and port a perform goes to. Disposing it closes every connection its resolvers kept.
/// </summary>
internal sealed class KeptDohResolver : IAsyncDisposable
{
    private readonly Lock gate = new();

    // The resolvers other settings have replaced, whose connections are not yet closed.
    private readonly List<DohDnsResolver> replaced = [];

    private (EasyNetworkOptions Network, EasyTlsOptions Tls, DohDnsResolver Resolver)? kept;

    /// <summary>
    /// Gives the resolver the transports built from <paramref name="network" /> and <paramref name="tls" /> use: the
    /// kept one when <c>--doh-url</c> and every other setting it was built from are unchanged, else the one
    /// <paramref name="create" /> builds, which is kept in its place when it is a <see cref="DohDnsResolver" />.
    /// </summary>
    /// <param name="network">The transfer's network options.</param>
    /// <param name="tls">The transfer's TLS options.</param>
    /// <param name="create">Builds the resolver the options pick.</param>
    /// <returns>The resolver.</returns>
    internal IDnsResolver ResolverFor(EasyNetworkOptions network, EasyTlsOptions tls, Func<IDnsResolver> create)
    {
        if (network.DohUrl is null)
        {
            return create();
        }

        EasyNetworkOptions settings = network with { Url = null, UrlHandle = null, Port = 0 };
        lock (gate)
        {
            if (kept is { } held && held.Network == settings && held.Tls == tls)
            {
                return held.Resolver;
            }

            IDnsResolver created = create();
            if (created is DohDnsResolver doh)
            {
                Release();
                kept = (settings, tls, doh);
            }

            return created;
        }
    }

    /// <summary>Closes the connections of the resolvers other settings have replaced, which no transfer asks any more.</summary>
    /// <returns>A task that completes once they are closed.</returns>
    internal async ValueTask CloseReplacedAsync()
    {
        DohDnsResolver[] closing;
        lock (gate)
        {
            closing = [.. replaced];
            replaced.Clear();
        }

        foreach (DohDnsResolver resolver in closing)
        {
            await resolver.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Closes the connections the kept resolver and any it replaced hold.</summary>
    /// <returns>A task that completes once they are closed.</returns>
    public ValueTask DisposeAsync()
    {
        lock (gate)
        {
            Release();
        }

        return CloseReplacedAsync();
    }

    // Moves the kept resolver, if there is one, to those to be closed; the caller holds the gate.
    private void Release()
    {
        if (kept is { } held)
        {
            replaced.Add(held.Resolver);
            kept = null;
        }
    }
}
