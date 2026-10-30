namespace Curl.Extension.Abstractions.Certificates;

/// <summary>
/// A check an activated extension adds to a server's certificate chain after curl's own verification
/// accepted it (ADR-1016) - DANE/TLSA, Certificate Transparency, ML-DSA certificates. A check can only
/// refuse a chain: one curl refused never reaches it.
/// </summary>
public interface ICertificateCheckHook
{
    /// <summary>Gets the option that activated the hook.</summary>
    ExtensionOption Option { get; }

    /// <summary>Checks the chain curl accepted.</summary>
    /// <param name="peerCertificates">The DER encoding of every certificate the server sent, its own first.</param>
    /// <param name="host">The host name curl verified the chain against.</param>
    /// <param name="port">The TCP port the checked connection was made to, as DANE needs for its TLSA name (RFC 6698 section 3), or <see langword="null" /> when an in-place upgrade's connection was not opened through the checking connector, so its port is unknown (ADR-1131).</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns><see langword="null" /> to let the chain stand, otherwise the message the transfer fails with, as exit 60.</returns>
    ValueTask<string?> RefusalOfAsync(IReadOnlyList<ReadOnlyMemory<byte>> peerCertificates, string host, int? port, CancellationToken cancellationToken);
}
