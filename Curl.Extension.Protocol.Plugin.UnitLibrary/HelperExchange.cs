using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.Plugin;

/// <summary>
/// One transfer's conversation with a helper, git's remote-helper model adapted (ADR-1142): Curl sends
/// <c>capabilities</c> and reads the helper's capability lines up to an empty one ("Capabilities"), then sends
/// <c>get &lt;url&gt;</c>, or <c>put &lt;url&gt;</c> followed by the upload, and closes the helper's input
/// ("Commands"); the helper answers <c>ok</c> and the body up to the end of its output, or <c>error &lt;message&gt;</c>.
/// </summary>
/// <param name="helper">The running helper.</param>
/// <param name="context">The transfer.</param>
/// <param name="helperName">The helper program's file name, for messages.</param>
internal sealed class HelperExchange(IHelperProcess helper, ITransferContext context, string helperName)
{
    /// <summary>The longest line a helper may send before its body, in bytes.</summary>
    internal const int MaximumLineLength = 8192;

    private const string Get = "get";

    private const string Put = "put";

    private const string ErrorReply = "error";

    /// <summary>Gets the helper program's file name.</summary>
    internal string HelperName => helperName;

    /// <summary>Runs the conversation.</summary>
    /// <returns>The transfer's outcome.</returns>
    /// <exception cref="HelperProtocolException">The helper broke the protocol or reported a failure.</exception>
    /// <exception cref="IOException">A pipe to the helper broke.</exception>
    internal async ValueTask<TransferResult> RunAsync()
    {
        context.Progress.ReportTransferStarted();
        await WriteLineAsync("capabilities").ConfigureAwait(false);
        IReadOnlySet<string> capabilities = await ReadCapabilitiesAsync().ConfigureAwait(false);
        bool isUpload = context.Upload is not null || context.PostData is not null;
        string command = isUpload ? Put : Get;
        if (!capabilities.Contains(command))
        {
            throw new HelperProtocolException(CurlExitCode.UnsupportedProtocol, $"{helperName} does not offer the {command} capability");
        }

        await WriteLineAsync($"{command} {context.Url.OriginalString}").ConfigureAwait(false);
        await WriteUploadAsync().ConfigureAwait(false);
        await helper.Input.DisposeAsync().ConfigureAwait(false);
        await ReadReplyAsync().ConfigureAwait(false);
        long bytes = await CopyBodyAsync().ConfigureAwait(false);
        int exitStatus = await helper.WaitForExitAsync(context.CancellationToken).ConfigureAwait(false);
        return exitStatus == 0
            ? TransferResult.Success(bytes)
            : TransferResult.Failure(CurlExitCode.RecvError, $"{helperName} exited with status {exitStatus}", bytes);
    }

    private async ValueTask<IReadOnlySet<string>> ReadCapabilitiesAsync()
    {
        HashSet<string> capabilities = new(StringComparer.Ordinal);
        while (await ReadLineAsync().ConfigureAwait(false) is { Length: > 0 } line)
        {
            // "Capabilities": a capability starting with '*' is mandatory, so one Curl does not know ends the transfer.
            string capability = line.TrimStart('*');
            if (line.StartsWith('*') && capability is not (Get or Put))
            {
                throw new HelperProtocolException(CurlExitCode.UnsupportedProtocol, $"{helperName} requires the unknown capability {capability}");
            }

            capabilities.Add(capability);
        }

        return capabilities;
    }

    private async ValueTask ReadReplyAsync()
    {
        string reply = await ReadLineAsync().ConfigureAwait(false) ?? throw Weird("closed its output without a reply");
        if (reply == ErrorReply || reply.StartsWith(ErrorReply + " ", StringComparison.Ordinal))
        {
            string message = reply.Length > ErrorReply.Length ? reply[(ErrorReply.Length + 1)..] : "error";
            throw new HelperProtocolException(CurlExitCode.RecvError, $"{helperName}: {message}");
        }

        if (reply != "ok")
        {
            throw Weird($"replied \"{reply}\"; expected ok or error");
        }
    }

    private async ValueTask WriteUploadAsync()
    {
        if (context.Upload is { } upload)
        {
            await upload.CopyToAsync(helper.Input, context.CancellationToken).ConfigureAwait(false);
        }
        else if (context.PostData is { } data)
        {
            await helper.Input.WriteAsync(data, context.CancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<long> CopyBodyAsync()
    {
        byte[] buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await helper.Output.ReadAsync(buffer, context.CancellationToken).ConfigureAwait(false)) > 0)
        {
            await context.Output.WriteAsync(buffer.AsMemory(0, read), context.CancellationToken).ConfigureAwait(false);
            total += read;
        }

        return total;
    }

    private async ValueTask WriteLineAsync(string line)
    {
        await helper.Input.WriteAsync(Encoding.UTF8.GetBytes(line + "\n"), context.CancellationToken).ConfigureAwait(false);
        await helper.Input.FlushAsync(context.CancellationToken).ConfigureAwait(false);
    }

    // A line ends at LF; a CR before it is dropped, as a Windows batch file's echo ends lines with CRLF.
    // Read a byte at a time, so the body after the reply line stays in the pipe for CopyBodyAsync.
    private async ValueTask<string?> ReadLineAsync()
    {
        List<byte> line = [];
        byte[] one = new byte[1];
        while (await helper.Output.ReadAsync(one, context.CancellationToken).ConfigureAwait(false) == 1 && one[0] != (byte)'\n')
        {
            line.Add(one[0]);
            if (line.Count > MaximumLineLength)
            {
                throw Weird($"sent a line longer than {MaximumLineLength} bytes");
            }
        }

        return line.Count == 0 && one[0] != (byte)'\n' ? null : Encoding.UTF8.GetString([.. line]).TrimEnd('\r');
    }

    private HelperProtocolException Weird(string what) => new(CurlExitCode.WeirdServerReply, $"{helperName} {what}");
}
