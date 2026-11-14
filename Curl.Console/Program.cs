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
        using Stream standardError = new WriteFailureIgnoringStream(StandardErrorOpener.Open());
        using Stream standardInput = Curl.Core.ClosedStandardInputStream.InPlaceOfNull(System.Console.OpenStandardInput());
        bool standardOutputIsTerminal = !System.Console.IsOutputRedirected;
        using StandardOutputVirtualTerminal virtualTerminal = StandardOutputVirtualTerminal.ForWindowsConsole();
        bool terminalRendersStyles = StandardOutputVirtualTerminal.RendersStyles(
            standardOutputIsTerminal, OperatingSystem.IsWindows(), virtualTerminal.Enable);

        // The arguments are copied out of argv first, so blanking argv's secrets leaves the run its credentials (BL-5093).
        IReadOnlyList<string> arguments = RawCommandLineArguments.ForCurrentPlatform(args);
        NativeArgumentBlanking.BlankForCurrentPlatform(arguments);

        return await UnhandledExceptionGuard.RunAsync(
                () => CurlComposition
                    .CreateRunner(standardOutput, standardError, standardInput, standardOutputIsTerminal, terminalRendersStyles)
                    .RunAsync(AnsiCodePageArguments.ForPlatform(arguments, OperatingSystem.IsWindows())),
                standardOutput,
                standardError)
            .ConfigureAwait(false);
    }
}
