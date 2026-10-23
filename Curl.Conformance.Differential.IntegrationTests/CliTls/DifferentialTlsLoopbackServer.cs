using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Curl.Conformance.Differential.Http.Core;
using Curl.Conformance.Differential.Tls;

namespace Curl.Conformance.Differential.IntegrationTests.CliTls;

/// <summary>
/// The C# port of the tester's <c>tls_server.py</c>: serves <see cref="DifferentialHttpResponder"/>'s
/// routes over TLS on every <see cref="DifferentialTlsEndpoints.All"/> port, each presenting its
/// <c>pki</c> certificate, accepting its TLS versions and, for <c>MTLS</c>, requiring a client
/// certificate that chains to <c>ca.pem</c>. A failed handshake ends only its connection.
/// </summary>
internal sealed class DifferentialTlsLoopbackServer : IAsyncDisposable
{
    private readonly List<TcpListener> _listeners = [];
    private readonly List<Task> _accepting = [];
    private readonly CancellationTokenSource _stopping = new();
    private readonly X509Certificate2 _authority;

    private DifferentialTlsLoopbackServer(string pkiDirectory)
    {
        _authority = X509CertificateLoader.LoadCertificateFromFile(Path.Combine(pkiDirectory, "ca.pem"));
        foreach (var endpoint in DifferentialTlsEndpoints.All)
        {
            var certificate = LoadServerCertificate(pkiDirectory, endpoint.CertificateName);
            var responder = new DifferentialHttpResponder(
                DifferentialRoutes.All, new DifferentialResponderState(), new DifferentialResponderOptions(endpoint.Port, TimeProvider.System));
            var listener = new TcpListener(IPAddress.Loopback, endpoint.Port);
            listener.Start();
            _listeners.Add(listener);
            _accepting.Add(AcceptAsync(listener, endpoint, certificate, responder, _stopping.Token));
        }
    }

    /// <summary>Starts every TLS server with the certificates in <paramref name="pkiDirectory"/>.</summary>
    public static DifferentialTlsLoopbackServer Start(string pkiDirectory) => new(pkiDirectory);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();
        _listeners.ForEach(listener => listener.Stop());
        await Task.WhenAll(_accepting);
        _stopping.Dispose();
        _authority.Dispose();
    }

    /// <summary>
    /// Loads <c>name.pem</c> with <c>name.key</c>; on Windows Schannel needs the key in a
    /// persisted key set, so the pair goes through a PKCS#12 round trip there.
    /// </summary>
    private static X509Certificate2 LoadServerCertificate(string pkiDirectory, string name)
    {
        var pem = X509Certificate2.CreateFromPemFile(Path.Combine(pkiDirectory, name + ".pem"), Path.Combine(pkiDirectory, name + ".key"));
        if (!OperatingSystem.IsWindows())
        {
            return pem;
        }

        using (pem)
        {
            return X509CertificateLoader.LoadPkcs12(pem.Export(X509ContentType.Pkcs12), null);
        }
    }

    private async Task AcceptAsync(
        TcpListener listener, DifferentialTlsEndpoint endpoint, X509Certificate2 certificate, DifferentialHttpResponder responder, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is OperationCanceledException or SocketException or ObjectDisposedException)
            {
                return;
            }

            _ = ServeAsync(client, endpoint, certificate, responder, cancellationToken);
        }
    }

    private async Task ServeAsync(
        TcpClient client, DifferentialTlsEndpoint endpoint, X509Certificate2 certificate, DifferentialHttpResponder responder, CancellationToken cancellationToken)
    {
        using (client)
        {
            try
            {
                await using var tls = new SslStream(client.GetStream(), leaveInnerStreamOpen: false,
                    (_, clientCertificate, _, _) => clientCertificate is null ? !endpoint.RequiresClientCertificate : IsSignedByAuthority(clientCertificate));
                await tls.AuthenticateAsServerAsync(
                    new SslServerAuthenticationOptions
                    {
                        ServerCertificate = certificate,
                        EnabledSslProtocols = endpoint.EnabledProtocols,
                        ClientCertificateRequired = endpoint.RequiresClientCertificate,
                        CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                    },
                    cancellationToken);
                await responder.RespondAsync(tls, cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or OperationCanceledException or SocketException or ObjectDisposedException
                or System.Security.Authentication.AuthenticationException or System.ComponentModel.Win32Exception)
            {
                // A failed handshake or a client that hangs up mid-response ends only its own connection.
            }
        }
    }

    /// <summary>Accepts a client certificate only when it chains to <c>ca.pem</c>, as <c>load_verify_locations</c> did.</summary>
    private bool IsSignedByAuthority(X509Certificate certificate)
    {
        using var custom = new X509Chain();
        custom.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        custom.ChainPolicy.CustomTrustStore.Add(_authority);
        custom.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        return custom.Build(X509CertificateLoader.LoadCertificate(certificate.GetRawCertData()));
    }
}
