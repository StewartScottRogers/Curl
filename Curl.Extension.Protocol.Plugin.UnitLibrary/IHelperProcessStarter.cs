namespace Curl.Extension.Protocol.Plugin;

/// <summary>Starts a helper program with its standard input and output piped to Curl.</summary>
public interface IHelperProcessStarter
{
    /// <summary>Starts <paramref name="program" /> with <paramref name="url" /> as its one argument.</summary>
    /// <param name="program">The helper program's path.</param>
    /// <param name="url">The URL, as it was typed.</param>
    /// <returns>The running helper.</returns>
    /// <exception cref="IOException">The program could not be started.</exception>
    IHelperProcess Start(string program, string url);
}
