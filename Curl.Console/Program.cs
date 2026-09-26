namespace Curl.Console;

/// <summary>
/// Entry point for the <c>curl</c> executable.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Builds the composition and runs the command line through it.
    /// </summary>
    /// <param name="args">The arguments as received from the shell.</param>
    /// <returns>The exit code, matching curl's <c>CURLE_</c> numbering.</returns>
    internal static async Task<int> Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        using Stream standardOutput = System.Console.OpenStandardOutput();
        using Stream standardError = System.Console.OpenStandardError();

        return await CurlComposition.CreateRunner(standardOutput, standardError)
            .RunAsync(args)
            .ConfigureAwait(false);
    }
}
