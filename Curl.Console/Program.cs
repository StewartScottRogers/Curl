namespace Curl.Console;

/// <summary>
/// Entry point for the <c>curl</c> executable.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Runs the command line.
    /// </summary>
    /// <param name="args">The arguments as received from the shell.</param>
    /// <returns>The exit code, matching curl's <c>CURLE_</c> numbering.</returns>
    internal static Task<int> Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        // Phase 1 wires the option parser, the dependency-injection container and the
        // transfer engine in here. Until then the executable exists so the solution
        // has a buildable, publishable target and the reference graph is real.
        System.Console.Error.WriteLine("curl: not implemented yet");

        return Task.FromResult((int)Protocol.Abstractions.CurlExitCode.FailedInit);
    }
}
