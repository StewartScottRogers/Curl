namespace Curl.Conformance.Differential.Http.Core;

/// <summary>Where a route sits in server.py's order: every specific route first, then <c>CONNECT</c>, then the default answer.</summary>
public enum DifferentialRouteRank
{
    /// <summary>A route for its own method or path; specific routes never overlap, so their order does not matter.</summary>
    Specific,

    /// <summary>The <c>CONNECT</c> route, tried after every specific route.</summary>
    Connect,

    /// <summary>The default answer, tried last.</summary>
    Default,
}
