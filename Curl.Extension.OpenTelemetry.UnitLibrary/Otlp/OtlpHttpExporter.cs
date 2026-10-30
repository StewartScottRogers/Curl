using System.Globalization;
using System.Text;

namespace Curl.Extension.OpenTelemetry.Otlp;

/// <summary>
/// Sends an OTLP/HTTP export request (OTLP specification 1.5.0, "OTLP/HTTP"): one <c>POST</c> of the JSON body to the
/// traces endpoint, used as given, over a connection curl's <see cref="IConnector" /> opens, with TLS for <c>https</c>.
/// A 2xx status is success ("Full Success"); any other answer, or none, is a failure, reported and never retried.
/// </summary>
/// <param name="connector">Opens the connection to the collector.</param>
internal sealed class OtlpHttpExporter(IConnector connector)
{
    private const int MaxStatusLineLength = 8192;

    /// <summary>Posts <paramref name="body" /> to <paramref name="endpoint" />.</summary>
    /// <param name="endpoint">The traces endpoint, such as <c>http://localhost:4318/v1/traces</c>.</param>
    /// <param name="body">The JSON <c>ExportTraceServiceRequest</c>.</param>
    /// <param name="cancellationToken">Cancels the export.</param>
    /// <returns><see langword="null" /> when the collector accepted the spans; otherwise why the export failed.</returns>
    internal async Task<string?> ExportAsync(string endpoint, byte[] body, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return $"the endpoint \"{endpoint}\" is not an http or https URL";
        }

        var connect = await connector.ConnectAsync(new ConnectTarget(uri.IdnHost, uri.Port, uri.Scheme == Uri.UriSchemeHttps), cancellationToken);
        if (connect.Connection is not { } connection)
        {
            return $"could not connect to {uri.IdnHost}: {connect.ErrorMessage}";
        }

        await using (connection)
        {
            try
            {
                await connection.WriteAsync(RequestBytes(uri, body), cancellationToken);
                await connection.FlushAsync(cancellationToken);
                return Outcome(await ReadStatusLineAsync(connection, cancellationToken));
            }
            catch (IOException failure)
            {
                return failure.Message;
            }
        }
    }

    private static byte[] RequestBytes(Uri endpoint, byte[] body)
    {
        var host = endpoint.HostNameType == UriHostNameType.IPv6 ? $"[{endpoint.IdnHost}]" : endpoint.IdnHost;
        var head = new StringBuilder()
            .Append("POST ").Append(endpoint.PathAndQuery).Append(" HTTP/1.1\r\n")
            .Append("Host: ").Append(endpoint.IsDefaultPort ? host : $"{host}:{endpoint.Port}").Append("\r\n")
            .Append("Content-Type: ").Append(OtlpJsonWriter.ContentType).Append("\r\n")
            .Append("Content-Length: ").Append(body.Length).Append("\r\n")
            .Append("Connection: close\r\n\r\n");
        return [.. Encoding.Latin1.GetBytes(head.ToString()), .. body];
    }

    private static async Task<string> ReadStatusLineAsync(IConnection connection, CancellationToken cancellationToken)
    {
        var line = new List<byte>();
        var buffer = new byte[1];
        while (line.Count < MaxStatusLineLength && await connection.ReadAsync(buffer, cancellationToken) == 1 && buffer[0] != '\n')
        {
            line.Add(buffer[0]);
        }

        return Encoding.Latin1.GetString([.. line]).TrimEnd('\r');
    }

    private static string? Outcome(string statusLine)
    {
        var parts = statusLine.Split(' ', 3);
        return parts.Length >= 2 && parts[0].StartsWith("HTTP/", StringComparison.Ordinal)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var status)
            ? status is >= 200 and < 300 ? null : $"the collector answered {statusLine}"
            : "the collector sent no HTTP response";
    }
}
