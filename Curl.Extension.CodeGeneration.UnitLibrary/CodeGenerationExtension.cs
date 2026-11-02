using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.CodeGeneration;

/// <summary>
/// The <c>--csharp</c> and <c>--powershell</c> extension (ADR-1138): as <c>--libcurl</c> writes a C program, they
/// write a C# program using <c>HttpClient</c> and a PowerShell script using <c>Invoke-RestMethod</c> that send the
/// requests the command line sends. The transfer itself runs unchanged. <c>Curl.Extension.Composition</c> registers
/// its options, description and transfer hook.
/// </summary>
public static class CodeGenerationExtension
{
    /// <summary>Gets the <c>--csharp</c> option.</summary>
    public static ExtensionOption CSharpOption { get; } = new(
        "csharp",
        ExtensionOptionArgument.Value,
        "Write C# using HttpClient that sends this command line's requests to <file>");

    /// <summary>Gets the <c>--powershell</c> option.</summary>
    public static ExtensionOption PowerShellOption { get; } = new(
        "powershell",
        ExtensionOptionArgument.Value,
        "Write a PowerShell script using Invoke-RestMethod that sends this command line's requests to <file>");

    /// <summary>Gets what <c>curl --extended-help</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "code-generation",
        "Writes the HTTP requests of a command line as C# using HttpClient (--csharp) or as a PowerShell script using Invoke-RestMethod (--powershell), the way --libcurl writes C.",
        [],
        [CSharpOption, PowerShellOption],
        new Uri("https://learn.microsoft.com/dotnet/api/system.net.http.httpclient"));

    /// <summary>Gives the extension's transfer hook when the option group gave <c>--csharp</c> or <c>--powershell</c>.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook, or <see langword="null" /> when neither option was given.</returns>
    public static ITransferHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        List<CodeFile> files = [];
        AddWhenGiven(files, values.ValueOf(CSharpOption), CSharpProgram.For);
        AddWhenGiven(files, values.ValueOf(PowerShellOption), PowerShellScript.For);
        return files.Count == 0 ? null : new CodeGenerationTransferHook(files);
    }

    private static void AddWhenGiven(List<CodeFile> files, string? path, Func<IReadOnlyList<GeneratedRequest>, string> writer)
    {
        if (path is not null)
        {
            files.Add(new CodeFile(path, writer));
        }
    }
}
