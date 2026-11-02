namespace Curl.Protocol.Abstractions;

/// <summary>
/// Starts the programs an extension's handler runs (a <c>curl-proto-&lt;scheme&gt;</c> helper, an
/// <c>mcp+stdio://</c> server) and reads the environment they are searched for and started in, so a host can
/// run those handlers against in-memory stand-ins (ADR-1183).
/// </summary>
public interface IExtensionProcessStarter
{
    /// <summary>Reads one variable of the environment programs are found and started in, such as <c>PATH</c>.</summary>
    /// <param name="name">The variable's name.</param>
    /// <returns>Its value, or <see langword="null" /> when it is not set.</returns>
    string? ReadEnvironmentVariable(string name);

    /// <summary>Starts <paramref name="program" /> with <paramref name="arguments" />, its standard input and output piped to Curl and its standard error left on Curl's.</summary>
    /// <param name="program">The program's name or path.</param>
    /// <param name="arguments">Its arguments, each passed as it is.</param>
    /// <returns>The running program.</returns>
    /// <exception cref="IOException">The program could not be started.</exception>
    IExtensionProcess Start(string program, IReadOnlyList<string> arguments);
}
