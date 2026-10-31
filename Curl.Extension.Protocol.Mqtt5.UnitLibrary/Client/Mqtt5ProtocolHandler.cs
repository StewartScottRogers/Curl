using System.Security.Cryptography;
using Curl.Extension.Protocol.Mqtt5.Packets;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.Mqtt5.Client;

/// <summary>
/// Serves <c>mqtt5://</c> (port 1883) and <c>mqtt5s://</c> (port 8883, TLS from the connector) with curl's
/// <c>mqtt://</c> behaviour in MQTT 5 (OASIS MQTT Version 5.0): with a body it publishes it to the URL's topic,
/// otherwise it subscribes to the topic and writes each message's payload to the output. <c>--user</c> goes into
/// CONNECT's User Name and Password. The exchange itself is <see cref="Mqtt5ClientFlow"/>, which takes the transport
/// it speaks over.
/// </summary>
public sealed class Mqtt5ProtocolHandler : IProtocolHandler
{
    private const string SecureScheme = "mqtt5s";

    private const int DefaultPort = 1883;

    private const int SecureDefaultPort = 8883;

    /// <summary>The start of every client identifier, as curl makes it.</summary>
    internal const string ClientIdentifierPrefix = "curl";

    private const string ClientIdentifierAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    private const int ClientIdentifierSuffixLength = 8;

    private readonly IConnector connector;

    private readonly Func<string> clientIdentifierSuffixSource;

    /// <summary>Creates the handler over <paramref name="connector"/>, with curl's random client identifier "curl" and eight letters or digits.</summary>
    public Mqtt5ProtocolHandler(IConnector connector)
        : this(connector, NewClientIdentifierSuffix)
    {
    }

    /// <summary>Creates the handler over <paramref name="connector"/>, taking the client identifier's suffix from <paramref name="clientIdentifierSuffixSource"/>.</summary>
    public Mqtt5ProtocolHandler(IConnector connector, Func<string> clientIdentifierSuffixSource)
    {
        this.connector = connector ?? throw new ArgumentNullException(nameof(connector));
        this.clientIdentifierSuffixSource = clientIdentifierSuffixSource ?? throw new ArgumentNullException(nameof(clientIdentifierSuffixSource));
    }

    /// <summary>The schemes served: <c>mqtt5</c> and <c>mqtt5s</c>.</summary>
    public IReadOnlyCollection<string> SupportedSchemes { get; } = ["mqtt5", SecureScheme];

    /// <summary>Connects to the URL's host and runs the publish or subscribe flow.</summary>
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            return await TransferAsync(context, Mqtt5UrlTopic.Decode(context.Url)).ConfigureAwait(false);
        }
        catch (Mqtt5ClientException failure)
        {
            return TransferResult.Failure(failure.ExitCode, failure.Message);
        }
    }

    /// <summary>Gives eight random letters or digits, the rest of curl's client identifier.</summary>
    internal static string NewClientIdentifierSuffix() => RandomNumberGenerator.GetString(ClientIdentifierAlphabet, ClientIdentifierSuffixLength);

    internal static ConnectTarget TargetOf(ITransferContext context)
    {
        CurlUrl url = context.Url;
        bool useTls = string.Equals(url.Scheme, SecureScheme, StringComparison.OrdinalIgnoreCase);
        int port = url.Port >= 0 ? url.Port : useTls ? SecureDefaultPort : DefaultPort;
        return new ConnectTarget(url.IdnHost, port, useTls)
        {
            Proxy = context.Proxy,
            Events = context.Events,
            DiagnosticLog = context.DiagnosticLog,
        };
    }

    internal static Mqtt5Connect ConnectFor(string clientIdentifier, System.Net.NetworkCredential? credentials) =>
        new()
        {
            ClientIdentifier = clientIdentifier,
            CleanStart = true,
            UserName = credentials?.UserName,
            Password = credentials is { Password.Length: > 0 } ? System.Text.Encoding.UTF8.GetBytes(credentials.Password) : null,
        };

    private async ValueTask<TransferResult> TransferAsync(ITransferContext context, string topic)
    {
        ConnectResult connect = await connector.ConnectAsync(TargetOf(context), context.CancellationToken).ConfigureAwait(false);
        if (connect.Connection is not { } connection)
        {
            return new TransferResult(connect.ExitCode, 0, connect.ErrorMessage) { IsConnectionRefused = connect.IsConnectionRefused };
        }

        context.Progress.ReportTransferStarted();
        await using (connection.ConfigureAwait(false))
        {
            var flow = new Mqtt5ClientFlow(connection, context.Output, context.Progress, context.TimeProvider, context.CancellationToken);
            try
            {
                await flow.RunAsync(ConnectFor(ClientIdentifierPrefix + clientIdentifierSuffixSource(), context.Credentials), topic, context.PostData).ConfigureAwait(false);
                return TransferResult.Success(flow.BytesWritten);
            }
            catch (Mqtt5ClientException failure)
            {
                return new TransferResult(failure.ExitCode, flow.BytesWritten, failure.Message);
            }
        }
    }
}
