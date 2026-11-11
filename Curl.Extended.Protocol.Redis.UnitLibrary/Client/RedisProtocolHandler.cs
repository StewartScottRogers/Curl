using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Redis.Client;

/// <summary>
/// Serves <c>redis://[user:password@]host[:port]/[db]/command/argument/...</c> and <c>rediss://</c> (TLS from the
/// connector), both on port 6379 by default (ADR-1079): one command, its reply written to the output. The command
/// comes from the URL's path (<see cref="RedisUrlCommand"/>), <c>-d</c> or <c>-T</c> appends one more argument, and
/// <c>--user</c> or the URL's user information authenticate. The exchange itself is <see cref="RedisClientFlow"/>.
/// </summary>
/// <param name="connector">Opens the connection to the server.</param>
internal sealed class RedisProtocolHandler(IConnector connector) : IProtocolHandler
{
    /// <summary>The port both schemes connect to when the URL names none, the redis URI scheme's default.</summary>
    internal const int DefaultPort = 6379;

    private const string SecureScheme = "rediss";

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes { get; } = ["redis", SecureScheme];

    /// <inheritdoc />
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return RedisUrlCommand.Parse(context.Url.AbsolutePath) is { } command
            ? await TransferAsync(context, command).ConfigureAwait(false)
            : TransferResult.Failure(CurlExitCode.UrlMalformat, RedisClientMessages.NoCommand);
    }

    /// <summary>Gives where to connect: the URL's host, its port or 6379, and TLS for <c>rediss</c>.</summary>
    internal static ConnectTarget TargetOf(ITransferContext context)
    {
        CurlUrl url = context.Url;
        bool useTls = string.Equals(url.Scheme, SecureScheme, StringComparison.OrdinalIgnoreCase);
        return new ConnectTarget(url.IdnHost, url.Port >= 0 ? url.Port : DefaultPort, useTls)
        {
            Proxy = context.Proxy,
            Events = context.Events,
            DiagnosticLog = context.DiagnosticLog,
        };
    }

    /// <summary>
    /// Gives the command's last argument: <c>-d</c>'s bytes, otherwise the whole of <c>-T</c>'s file, otherwise none,
    /// so an upload is sent as the value exactly as <c>-d</c> is and never turns the command into a fetch.
    /// </summary>
    private static async ValueTask<ReadOnlyMemory<byte>?> RequestBodyAsync(ITransferContext context)
    {
        if (context.PostData is not null || context.Upload is not { } upload)
        {
            return context.PostData;
        }

        using var body = new MemoryStream();
        await upload.CopyToAsync(body, context.CancellationToken).ConfigureAwait(false);
        return body.ToArray();
    }

    private async ValueTask<TransferResult> TransferAsync(ITransferContext context, RedisUrlCommand command)
    {
        ConnectResult connect = await connector.ConnectAsync(TargetOf(context), context.CancellationToken).ConfigureAwait(false);
        if (connect.Connection is not { } connection)
        {
            return new TransferResult(connect.ExitCode, 0, connect.ErrorMessage) { IsConnectionRefused = connect.IsConnectionRefused };
        }

        context.Progress.ReportTransferStarted();
        await using (connection.ConfigureAwait(false))
        {
            var flow = new RedisClientFlow(connection, context.Output, context.Progress, context.CancellationToken);
            try
            {
                await flow.RunAsync(command, context.Credentials, await RequestBodyAsync(context).ConfigureAwait(false)).ConfigureAwait(false);
                return TransferResult.Success(flow.BytesWritten);
            }
            catch (RedisClientException failure)
            {
                return new TransferResult(failure.ExitCode, flow.BytesWritten, failure.Message);
            }
        }
    }
}
