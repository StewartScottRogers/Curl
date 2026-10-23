namespace Curl.Conformance.Differential.Http.Core;

/// <summary>
/// What routes need from the run: the port the responder listens on (some routes embed it in a
/// <c>Location</c>) and the clock their delays wait on.
/// </summary>
public sealed record DifferentialResponderOptions(int Port, TimeProvider TimeProvider);
