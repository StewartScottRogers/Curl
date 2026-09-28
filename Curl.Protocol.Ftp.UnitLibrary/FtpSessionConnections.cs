using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp;

/// <summary>
/// The seams an <see cref="FtpSession" /> opens and secures its connections through.
/// </summary>
/// <param name="Connector">Opens a passive-mode data connection.</param>
/// <param name="Listener">Binds the port an active-mode data connection is accepted on.</param>
/// <param name="TlsProvider">Upgrades the control connection after <c>AUTH</c>, and data connections after <c>PROT P</c>.</param>
internal sealed record FtpSessionConnections(IConnector Connector, IConnectionListener Listener, ITlsProvider TlsProvider);
