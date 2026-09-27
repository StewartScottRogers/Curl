using System.Security.Cryptography.X509Certificates;

namespace Curl.Output;

/// <summary>
/// The three certificates a loopback <c>openssl s_server</c> sent on 2026-09-26, and the
/// <c>%{certs}</c> text curl 8.21.0 (mingw, Schannel) printed for them; the commands are
/// in BL-303's Notes.
/// </summary>
internal static class LoopbackChain
{
    /// <summary>Gets the DER encodings, the server's certificate first, in the order sent.</summary>
    internal static ReadOnlyMemory<byte>[] Certificates { get; } = ReadCertificates();

    /// <summary>Gets what curl printed for <c>%{certs}</c>, with line feeds as curl writes them before text mode.</summary>
    internal static string CertsText { get; } =
        File.ReadAllText(FixturePath("LoopbackChain.certs.txt")).Replace("\r", string.Empty, StringComparison.Ordinal);

    private static ReadOnlyMemory<byte>[] ReadCertificates()
    {
        var collection = new X509Certificate2Collection();
        collection.ImportFromPemFile(FixturePath("LoopbackChain.pem"));
        return [.. collection.Select(certificate => new ReadOnlyMemory<byte>(certificate.RawData))];
    }

    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
}
