namespace Curl.Kerberos;

/// <summary>One KDC to send a request to.</summary>
/// <param name="Transport">How it is reached.</param>
/// <param name="Host">Its host name or address, without IPv6 brackets.</param>
/// <param name="Port">Its port: 88 when a <c>kdc</c> entry names none, 443 for HTTPS.</param>
/// <param name="HttpsPath">For <see cref="KerberosKdcTransport.Https" />, the proxy's path without its leading slash; otherwise empty.</param>
public sealed record KerberosKdcAddress(KerberosKdcTransport Transport, string Host, int Port, string HttpsPath = "");
