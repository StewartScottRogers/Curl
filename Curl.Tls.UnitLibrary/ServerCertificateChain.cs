namespace Curl.Tls;

/// <summary>The certificate chain a server presented, as <see cref="IServerCertificateVerifier" /> receives it.</summary>
/// <param name="Certificates">The DER certificates in the order the server sent them, leaf first.</param>
/// <param name="HostName">The name the client offered in <c>server_name</c>, or <see langword="null" /> when it offered none.</param>
/// <param name="OcspResponse">The OCSP response stapled to the leaf, or <see langword="null" /> when none was.</param>
public sealed record ServerCertificateChain(IReadOnlyList<byte[]> Certificates, string? HostName, byte[]? OcspResponse);
