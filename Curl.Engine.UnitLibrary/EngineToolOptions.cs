namespace Curl.Engine;

/// <summary>
/// The settings the curl tool adds to its option groups that libcurl's easy options have no
/// <c>CURLOPT_*</c> for, and that the engine's transport factories read: the host (<c>Curl.Console</c>
/// from the command line) builds it and hands it to the engine.
/// </summary>
/// <param name="Parallel"><c>-Z</c>: the transfers run in parallel.</param>
/// <param name="ParallelImmediate"><c>--parallel-immediate</c>: a parallel transfer does not wait for a connection that may multiplex.</param>
/// <param name="SslSessionsFile">The <c>--ssl-sessions</c> file, or <see langword="null" /> for none.</param>
/// <param name="MaxTime">The <c>-m</c> limit the <c>--trace-config</c> timer lines report, or <see langword="null" /> for none.</param>
public sealed record EngineToolOptions(bool Parallel, bool ParallelImmediate, string? SslSessionsFile, TimeSpan? MaxTime);
