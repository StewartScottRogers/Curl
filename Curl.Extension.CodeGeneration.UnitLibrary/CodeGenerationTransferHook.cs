using System.Text;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.CodeGeneration;

/// <summary>
/// The <c>--csharp</c> and <c>--powershell</c> transfer hook (ADR-1128). Before each transfer of its option group it
/// adds the transfer's request to those it has seen and writes every file again, so a command line with several URLs
/// gives one program sending them in order, as <c>--libcurl</c> does. A file it cannot write is reported on the
/// transfer's diagnostic log, as <c>--libcurl</c> warns, and the transfer runs unchanged.
/// </summary>
/// <param name="files">The files the options asked for.</param>
internal sealed class CodeGenerationTransferHook(IReadOnlyList<CodeFile> files) : ITransferHook
{
    private readonly List<GeneratedRequest> requests = [];

    /// <summary>Gets the files the options asked for.</summary>
    internal IReadOnlyList<CodeFile> Files => files;

    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        requests.Add(GeneratedRequest.From(context));
        foreach (var file in files)
        {
            await WriteAsync(file, context).ConfigureAwait(false);
        }

        return await next(context).ConfigureAwait(false);
    }

    private async Task WriteAsync(CodeFile file, ITransferContext context)
    {
        try
        {
            await File.WriteAllTextAsync(file.Path, file.Writer(requests), new UTF8Encoding(false), context.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            context.DiagnosticLog.Write(DiagnosticLogLevel.Error, "code-generation", "Failed to open " + file.Path + " to write code");
        }
    }
}
