namespace Curl.Conformance.Differential.Http.Core;

/// <summary>What a route answers with: the request, the connection to write to, the per-run state and the options.</summary>
public sealed record DifferentialRouteContext(
    DifferentialHttpRequest Request,
    Stream Connection,
    DifferentialResponderState State,
    DifferentialResponderOptions Options);
