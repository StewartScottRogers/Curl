using Curl.Api.Options.Network;
using Curl.Api.Options.Tls;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// The <c>--doh-url</c> resolvers kept beside one connection cache from perform to perform, so a later perform over
/// that cache asks the DoH server over the connections an earlier one opened, whichever handle ran it, as libcurl keeps
/// them in its connection cache (ADR-1453, ADR-1454). There is one resolver for each set of options one was built from:
/// the TLS options, and the network options but for the URL, the port and the <c>--connect-to</c> mappings a perform
/// goes to. At most <see cref="MostKept" /> are kept, the one asked for longest ago being closed to make room.
/// Disposing it closes every connection its resolvers kept; it keeps new ones afterwards.
/// </summary>
internal sealed class KeptDohResolver : IAsyncDisposable
{
    /// <summary>How many resolvers are kept at once.</summary>
    internal const int MostKept = 4;

    private readonly Lock gate = new();

    // The one asked for longest ago first.
    private readonly List<Kept> kept = [];

    // The closing of the resolvers dropped to make room.
    private Task droppedClosing = Task.CompletedTask;

    /// <summary>
    /// Gives the resolver the transports built from <paramref name="network" /> and <paramref name="tls" /> use: the
    /// kept one built from the same <c>--doh-url</c> and other settings, else the one <paramref name="create" />
    /// builds, which is kept when it is a <see cref="DohDnsResolver" />.
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

        // The lists compare by reference in a record, so --resolve's entries are compared one by one.
        EasyNetworkOptions settings = network with { Url = null, UrlHandle = null, Port = 0, ConnectTo = [], Resolve = [] };
        string[] resolveEntries = [.. network.Resolve];
        lock (gate)
        {
            int index = kept.FindIndex(held => held.Network == settings && held.Tls == tls && held.ResolveEntries.SequenceEqual(resolveEntries));
            if (index >= 0)
            {
                Kept held = kept[index];
                kept.RemoveAt(index);
                kept.Add(held);
                return held.Resolver;
            }

            IDnsResolver created = create();
            if (created is DohDnsResolver doh)
            {
                kept.Add(new Kept(settings, resolveEntries, tls, doh));
                DropBeyondMostKept();
            }

            return created;
        }
    }

    /// <summary>Closes the connections the kept resolvers and those dropped to make room hold.</summary>
    /// <returns>A task that completes once they are closed.</returns>
    public async ValueTask DisposeAsync()
    {
        Kept[] closing;
        Task dropped;
        lock (gate)
        {
            closing = [.. kept];
            kept.Clear();
            dropped = droppedClosing;
            droppedClosing = Task.CompletedTask;
        }

        foreach (Kept held in closing)
        {
            await held.Resolver.DisposeAsync().ConfigureAwait(false);
        }

        await dropped.ConfigureAwait(false);
    }

    // Starts closing the resolver asked for longest ago once there are too many; the caller holds the gate. A
    // transfer still asking it is not cut off: a disposed resolver closes each connection as it is done with it.
    private void DropBeyondMostKept()
    {
        if (kept.Count > MostKept)
        {
            DohDnsResolver oldest = kept[0].Resolver;
            kept.RemoveAt(0);
            droppedClosing = Task.WhenAll(droppedClosing, oldest.DisposeAsync().AsTask());
        }
    }

    // A kept resolver and the options it was built from.
    private sealed record Kept(EasyNetworkOptions Network, string[] ResolveEntries, EasyTlsOptions Tls, DohDnsResolver Resolver);
}
