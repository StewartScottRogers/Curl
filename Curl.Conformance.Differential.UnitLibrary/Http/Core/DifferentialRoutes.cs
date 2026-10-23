namespace Curl.Conformance.Differential.Http.Core;

/// <summary>
/// The routes every route file has added from its <c>[ModuleInitializer]</c>: no central switch and no
/// reflection, so it stays Native AOT safe.
/// </summary>
public static class DifferentialRoutes
{
    private static readonly List<IDifferentialRoute> Registered = [];

    /// <summary>Gets every registered route in the order they are tried: by <see cref="DifferentialRouteRank"/>, then registration order.</summary>
    public static IReadOnlyList<IDifferentialRoute> All => [.. Registered.OrderBy(route => route.Rank)];

    /// <summary>Adds <paramref name="route"/>; called only from a route file's module initializer.</summary>
    internal static void Register(IDifferentialRoute route) => Registered.Add(route);
}
