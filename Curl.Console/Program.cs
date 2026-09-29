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

        using Stream standardOutput = StandardOutputOpener.Open();
        using Stream standardError = System.Console.OpenStandardError();
        using Stream standardInput = System.Console.OpenStandardInput();
        bool standardOutputIsTerminal = !System.Console.IsOutputRedirected;
        using StandardOutputVirtualTerminal virtualTerminal = StandardOutputVirtualTerminal.ForWindowsConsole();
        bool terminalRendersStyles = StandardOutputVirtualTerminal.RendersStyles(
            standardOutputIsTerminal, OperatingSystem.IsWindows(), virtualTerminal.Enable);

        return await CurlComposition
            .CreateRunner(standardOutput, standardError, standardInput, standardOutputIsTerminal, terminalRendersStyles)
            .RunAsync(args)
            .ConfigureAwait(false);
    }
}
