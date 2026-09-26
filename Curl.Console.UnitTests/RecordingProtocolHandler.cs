using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// A protocol handler for one scheme that records every context it receives and answers
/// with whatever <c>execute</c> returns.
/// </summary>
internal sealed class RecordingProtocolHandler(
    string scheme,
    Func<ITransferContext, ValueTask<TransferResult>> execute) : IProtocolHandler
{
    public List<ITransferContext> Contexts { get; } = [];

    public IReadOnlyCollection<string> SupportedSchemes { get; } = [scheme];

    public ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        Contexts.Add(context);

        return execute(context);
    }

    /// <summary>A handler that writes the URL's path bytes to the output and succeeds.</summary>
    public static RecordingProtocolHandler WritingPath(string scheme) =>
        new(scheme, async context =>
        {
            byte[] bytes = System.Text.Encoding.ASCII.GetBytes(context.Url.AbsolutePath);
            await context.Output.WriteAsync(bytes, context.CancellationToken);

            return TransferResult.Success(bytes.Length);
        });

    /// <summary>A handler that fails every transfer with <paramref name="exitCode" /> and <paramref name="message" />.</summary>
    public static RecordingProtocolHandler Failing(string scheme, CurlExitCode exitCode, string message) =>
        new(scheme, _ => ValueTask.FromResult(TransferResult.Failure(exitCode, message)));
}
