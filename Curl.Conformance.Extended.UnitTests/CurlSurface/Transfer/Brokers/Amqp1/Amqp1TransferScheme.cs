namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Brokers.Amqp1;

/// <summary>
/// One AMQP 1.0 scheme as the transfer-surface checks drive it: <c>amqp1</c> in clear on 5672 or <c>amqp1s</c> over TLS on
/// 5671 (ADR-1062), and the number its requirement IDs end with.
/// </summary>
/// <param name="Scheme">The URL scheme.</param>
/// <param name="Port">The port the URL names and the route serves.</param>
/// <param name="Tls">Whether the route serves the connection over TLS.</param>
/// <param name="Number">The number the scheme's <c>CurlSurface:Amqp1&lt;option&gt;-&lt;n&gt;</c> IDs end with.</param>
internal sealed record Amqp1TransferScheme(string Scheme, int Port, bool Tls, int Number)
{
    public static readonly Amqp1TransferScheme Amqp1 = new("amqp1", 5672, Tls: false, 1);

    public static readonly Amqp1TransferScheme Amqp1s = new("amqp1s", 5671, Tls: true, 2);

    /// <summary>The URL every check transfers: the address <c>queue</c>, whose name <c>-O</c> names the file after.</summary>
    public string Url => $"{Scheme}://localhost:{Port}/queue";

    /// <summary>The requirement ID of <paramref name="option" /> for this scheme.</summary>
    public string Requirement(string option) => $"CurlSurface:Amqp1{option}-{Number}";
}
