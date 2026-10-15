namespace Curl.Engine;

/// <summary>
/// The run's <c>--trace-config</c> components: curl's <c>curl_global_trace</c> setting, a global rather than an
/// easy option, so the host builds it (<c>Curl.Console</c> from the command line) and hands it to the engine.
/// </summary>
/// <param name="Components">Every trace component that is on, including the ones <c>-vv</c> and up put there.</param>
/// <param name="VerbosityComponents">The names in <paramref name="Components" /> that only <c>-vv</c> and up put there, not a <c>--trace-config</c>.</param>
public sealed record EngineTraceComponents(IReadOnlySet<string> Components, IReadOnlySet<string> VerbosityComponents)
{
    /// <summary>No trace component is on.</summary>
    public static EngineTraceComponents None { get; } = new(new HashSet<string>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));
}
