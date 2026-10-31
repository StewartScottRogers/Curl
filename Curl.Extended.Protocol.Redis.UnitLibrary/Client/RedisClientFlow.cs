using System.Net;
using Curl.Extended.Protocol.Redis.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Redis.Client;

/// <summary>
/// The Redis exchange over any byte transport (ADR-1079): <c>HELLO 3</c> (with <c>AUTH</c> when there are
/// credentials) to speak RESP3, <c>SELECT</c> when the URL names a database, then the command, its reply written to
/// the output by <see cref="RespReplyRendering"/>. An error reply to <c>HELLO</c> with credentials or a
/// <c>NOAUTH</c> error ends with exit 67; any other error reply, or a malformed reply, with exit 8.
/// </summary>
internal sealed class RedisClientFlow(IConnection transport, Stream output, ITransferProgress progress, CancellationToken cancellationToken)
{
    /// <summary>The user name <c>HELLO</c>'s <c>AUTH</c> sends when only a password is given, as <c>AUTH password</c> means.</summary>
    internal const string DefaultUserName = "default";

    private readonly RespReplyReader reader = new(transport, cancellationToken);

    /// <summary>The bytes written to the output so far.</summary>
    internal long BytesWritten { get; private set; }

    /// <summary>Runs the flow for <paramref name="command"/>, with <paramref name="body"/> as its last argument when there is one.</summary>
    internal async ValueTask RunAsync(RedisUrlCommand command, NetworkCredential? credentials, ReadOnlyMemory<byte>? body)
    {
        RespReply hello = await ExchangeAsync(HelloFor(credentials), "HELLO").ConfigureAwait(false);
        if (hello.IsError)
        {
            bool isLoginDenied = credentials is not null || hello.Text.StartsWith("NOAUTH", StringComparison.Ordinal);
            throw new RedisClientException(isLoginDenied ? CurlExitCode.LoginDenied : CurlExitCode.WeirdServerReply, RedisClientMessages.ServerError("HELLO", hello.Text));
        }

        if (command.Database is { } database)
        {
            ThrowIfError(await ExchangeAsync(RespCommandEncoding.Arguments("SELECT", database), "SELECT").ConfigureAwait(false), "SELECT");
        }

        RespReply reply = await ExchangeAsync(command.ArgumentsWith(body), command.Name).ConfigureAwait(false);
        ThrowIfError(reply, command.Name);
        await WriteOutputAsync(RespReplyRendering.Render(reply)).ConfigureAwait(false);
    }

    /// <summary>Gives <c>HELLO 3</c>, with <c>AUTH</c> and the user name (or <see cref="DefaultUserName"/>) and password when there are credentials.</summary>
    internal static IReadOnlyList<byte[]> HelloFor(NetworkCredential? credentials) => credentials is null
        ? RespCommandEncoding.Arguments("HELLO", "3")
        : RespCommandEncoding.Arguments("HELLO", "3", "AUTH", credentials.UserName.Length == 0 ? DefaultUserName : credentials.UserName, credentials.Password);

    private static void ThrowIfError(RespReply reply, string command)
    {
        if (reply.IsError)
        {
            throw new RedisClientException(CurlExitCode.WeirdServerReply, RedisClientMessages.ServerError(command, reply.Text));
        }
    }

    private async ValueTask<RespReply> ExchangeAsync(IReadOnlyList<byte[]> arguments, string command)
    {
        try
        {
            await transport.WriteAsync(RespCommandEncoding.Encode(arguments), cancellationToken).ConfigureAwait(false);
            await transport.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            throw new RedisClientException(CurlExitCode.SendError, RedisClientMessages.SendFailed(command));
        }

        return await ReceiveAsync().ConfigureAwait(false);
    }

    private async ValueTask<RespReply> ReceiveAsync()
    {
        try
        {
            return await reader.ReadAsync().ConfigureAwait(false)
                ?? throw new RedisClientException(CurlExitCode.RecvError, RedisClientMessages.Disconnected);
        }
        catch (FormatException malformed)
        {
            throw new RedisClientException(CurlExitCode.WeirdServerReply, malformed.Message);
        }
        catch (IOException)
        {
            throw new RedisClientException(CurlExitCode.RecvError, RedisClientMessages.Disconnected);
        }
    }

    private async ValueTask WriteOutputAsync(byte[] rendered)
    {
        try
        {
            await output.WriteAsync(rendered, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            throw new RedisClientException(CurlExitCode.WriteError, RedisClientMessages.OutputWriteFailed(rendered.Length));
        }

        BytesWritten += rendered.Length;
        progress.ReportDownloaded(BytesWritten, null);
    }
}
