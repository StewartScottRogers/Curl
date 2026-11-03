using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Extended.Plugin;

/// <summary>
/// The stand-in process starter a Plugin case passes to curl: it gives the helper search the environment it was given
/// (<c>PATH</c>, <c>PATHEXT</c>), records each program and arguments curl starts, and hands back <see cref="PluginReferenceHelper" />,
/// or refuses the start as a missing program is refused when it has none.
/// </summary>
/// <param name="environment">The variables the helper search reads.</param>
/// <param name="helper">The helper every start returns; <see langword="null" /> to refuse every start.</param>
public sealed class PluginReferenceHelperStarter(IReadOnlyDictionary<string, string> environment, PluginReferenceHelper? helper) : IExtensionProcessStarter
{
    /// <summary>The reason a refused start gives.</summary>
    public const string StartFailure = "The system cannot find the file specified.";

    private readonly List<PluginHelperStart> starts = [];

    /// <summary>Gets every start curl asked for, in order.</summary>
    public IReadOnlyList<PluginHelperStart> Starts => starts;

    /// <inheritdoc />
    public string? ReadEnvironmentVariable(string name) => environment.GetValueOrDefault(name);

    /// <inheritdoc />
    public IExtensionProcess Start(string program, IReadOnlyList<string> arguments)
    {
        starts.Add(new PluginHelperStart(program, [.. arguments]));
        return helper ?? throw new IOException(StartFailure);
    }
}
