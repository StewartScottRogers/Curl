# Curl.Original.Protocol.Ssh.UnitLibrary

Phase 2.

File transfer over SSH.

**URL schemes:** `scp`, `sftp`

This library may reference `Curl.Protocol.Abstractions.UnitLibrary` and the
hand-built libraries ADR-0120 lists; it references `Curl.Cryptography.UnitLibrary`
for `FiniteFieldDiffieHellman`, `DsaSignature`, `Des`, `X25519`, `Ed25519`, `ChaCha20`, `Poly1305`, `BcryptPbkdf` and `AesCtr`. Referencing another protocol
library is a build break, and `Curl.Protocol.Abstractions.UnitTests` fails if one
appears.

`SshProtocolHandler`, at the root, is the library's `IProtocolHandler`: it narrows the
host-key list from the known-hosts file, connects through the injected `IConnector`,
runs the handshake, requests `ssh-userauth`, checks the host key, authenticates, runs
`SftpFileUpload` or `ScpFileUpload` for a transfer with an upload, and otherwise `SftpFileDownload`
or `ScpFileDownload` - or `SftpDirectoryListing` for an `sftp` path
ending with a slash - into the output, and after a success leaves the session
(`SshKeptSession`, with its SFTP session and home directory in `SftpSessionSlot`) on the
connection for the next URL of the same login, which skips the handshake and the
authentication (ADR-0865); a failure, or the connection cache's close, ends the session with
`DISCONNECT` 11 `Shutdown`; every `SshTransferException` becomes a failed
`TransferResult`. On the way it reports curl's `-v` lines, worded in `SshInfoLines`, through
`ITransferEvents`, and each block written to the output through
`ReceivedDataReportingStream` (ADR-0262). Curl's own diagnostic log (component `ssh`,
ADR-0222, BL-925) is worded in `SshDiagnosticLog`, which the handler builds over the
`Curl.Ssh` transport's `SshTransportLog` it hangs on `SshTransport.DiagnosticLog`
(ADR-1165), so the packet reader and writer, the authentication, the channel and the SFTP
session all write to the same log;
a new SSH step logs through it, never a password, pass phrase or key byte. The console passes the platform's preset and ADR-0022's credential
encoding. `Curl.Original.Protocol.Ssh.UnitTests` drives it end to end against the public
`Fakes.InMemorySshServer`, an `IConnector` answering each connection with a real
server-side session over `Fakes.InMemoryDuplexConnection`. `SshWireDecoders`, also at
the root, runs the readers of server bytes - packets, zlib, `KEXINIT`, SFTP attributes,
host-key signatures - over raw bytes for the audit fuzzer (ADR-0394); a new reader of
what a server sends gets a method there.

Folders follow ADR-0122's structure. The transport layer - the `Transport`, `Negotiation`,
`KeyExchange`, `PacketProtection` and `Compression` folders, the host-key signature verifiers
of `HostKeys` and the root `ISshRandomSource`, `SshConnectionLostException`,
`Libssh2ErrorCode` and `SshTransferException` - lives in `Curl.Ssh.UnitLibrary` (namespaces
`Curl.Ssh.*`, ADR-1165) so extensions can use it; this library keeps the rest. There,
`Transport` frames packets and runs the
handshake (`SshTransport`: identification, `KEXINIT`, key exchange, `NEWKEYS`, a
server's re-exchange); `Negotiation` holds the presets - each with the prime sizes group
exchange asks for, (2048, 4096, 4096) on Windows and (2048, 4096, 8192) on OpenSSL
(ADR-0268) - and the catalogue of
implemented names; `KeyExchange` holds one `ISshKeyExchange` per method family - the hybrid post-quantum
methods in `HybridKemSshKeyExchange`, which pairs two `ISshKeyShare`s (ML-KEM or sntrup761
with X25519 or a NIST curve) and hashes K as a `string` (ADR-0265) - and `SshKeyDerivation`; `HostKeys` holds one `ISshSignatureVerifier` per host-key type - a certificate checked with its certified key alone by `OpenSshCertificateSshSignatureVerifier`, as libssh2 does, and the `sk-` keys over `SecurityKeySignedData` (ADR-0266) - while this library's `HostKeys`
holds `KnownHostsFile` and `SshHostKeyChecker`, which accept or refuse the host key (ADR-0213);
`PacketProtection` holds one `ISshPacketProtection` per cipher family, which the packet
reader and writer switch to at each `NEWKEYS` (ADR-0212) - `ChaCha20Poly1305PacketProtection`
(ADR-0259), `AesGcmPacketProtection` and `CipherAndMacPacketProtection`; `Compression` holds
`SshZlibCompressor` and `SshZlibDecompressor`, one BCL `ZLibStream` per direction for the
session, which the writer and reader start at the first `NEWKEYS` for `zlib` and, through
`SshTransport.StartDelayedCompression`, after `SSH_MSG_USERAUTH_SUCCESS` for
`zlib@openssh.com` (ADR-0264); `Authentication` holds
`SshUserAuthentication`, which requests the `ssh-userauth` service and authenticates the
user with `none`, `publickey`, `password`, the ssh-agent's identities and
`keyboard-interactive` in curl's order (ADR-0215, ADR-0230, ADR-0271), upgrading an `ssh-rsa` key, and on OpenSSL an RSA certificate, from `server-sig-algs` (ADR-0311) - the agent through
`ISshAgentConnector` (`PlatformSshAgentConnector`: on Windows PuTTY's Pageant through
`PageantSshAgentConnector`, then `SystemSshAgentConnector`'s pipe; elsewhere the Unix socket
`SSH_AUTH_SOCK` names - ADR-0304) and `SshAgentClient`, which speaks the agent protocol; `Keys` holds `SshUserKeySource`, which finds `--key` (or curl's
default files through an injected `HOME` reader), `--pubkey` and `--pass`,
`SshPrivateKeyReader`, which reads every PEM, PKCS #8 and `openssh-key-v1` key file
ADR-0122 lists, Ed25519 and bcrypt-encrypted `openssh-key-v1` included
(`OpenSshPrivateSectionDecryption`, ADR-0263), and one `SshPrivateKey` per key type
(`RsaSshPrivateKey`, `EcdsaSshPrivateKey`, `DsaSshPrivateKey`, `Ed25519SshPrivateKey`), which signs;
`Curl.Ssh`'s `Connection` (ADR-1166) holds `SshChannel`, one RFC 4254 channel with libssh2's
window and packet size, logged through `SshChannelLog`; `SshSessionChannel`, the `session`
channel, which starts a subsystem or an `exec` command; and `SshDirectTcpipChannel`, a
`direct-tcpip` channel read and written as an `IConnection`. Here, `Scp` holds
`ScpFileDownload`, which runs `scp -pf` (`ScpCommand`, `ScpRemotePath`), reads its `T`
and `C` lines with libssh2's checks (`ScpFileHeaderReader`, `ScpHeaderNumber`) and copies
the file (ADR-0225), and `ScpFileUpload`, which runs `scp -t`, sends the `C` line with
`--create-file-mode` and the bytes between `scp`'s acknowledgements, and refuses a source
of unknown size (ADR-0258); `Sftp` holds `SftpSession`, the SFTP version 3 client over that
channel, and `SftpFileDownload`, which downloads one file with `SftpReadAhead`'s reads in
flight and maps each `SSH_FX_*` status to curl's exit code through `SftpStatusCode`
(ADR-0220) - only the `SftpDownloadPart` that `-r` and `-C` ask for (ADR-0253) - and `SftpDirectoryListing`, which lists a directory with `OPENDIR` and
`READDIR` as curl prints it - each `SftpDirectoryEntry`'s long name, a symbolic link
followed with `READLINK`, or the names alone with `-l` (ADR-0241), and `SftpFileUpload`,
which uploads with the `SftpOpenFlags` `SftpUploadOptions` call for (`-C`, `-C -`, `-a`,
`--create-file-mode`), makes missing directories with `MKDIR` under `--ftp-create-dirs`,
and sends 64 KiB blocks as `WRITE`s of 30000 bytes (ADR-0244). All three run the `-Q`
commands through `SftpQuoteCommands`: those with no prefix after `REALPATH .`, those with a
`-` after a successful transfer's `CLOSE`. `SftpQuoteCommand` reads each command as curl's
`sftp_quote` does, and `SftpAttributes` carries what `STAT` and `SETSTAT` exchange
(ADR-0247). A new algorithm registers in
`SshKeyExchangeMethods`, `SshSignatureVerifiers`, `SshPacketProtections` or `SshCompressionMethods`, and
`SshAlgorithmCatalogue.Implemented` offers it from then on. Failure messages and
their libssh2 codes are ADR-0122's, ADR-0206's and ADR-0212's, measured from the
reference builds. A failed read or write of the connection, such as a reset, reaches
the steps as an `SshConnectionLostException`, so `SshConnectionFailure.Is` tells it
apart from the local output's `IOException`; each step reports it as libssh2 does
(ADR-0283 for the handshake, ADR-0289 after it). A download's write to the local output goes
through `SshOutputWrite`, which turns that output's `IOException` (an `-o` file that cannot be
created) into exit 23, `Failure writing output to destination, passed N returned M`, so the
session still closes and sends `DISCONNECT` and the curl tool still prints `-w` (CGR-133, BL-3662).

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnection`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network. The one exception is the local ssh-agent:
`SystemSshAgentConnector` opens its named pipe or Unix domain socket, behind
`ISshAgentConnector`, in two methods excluded from coverage under ADR-0083 (ADR-0271),
and `WindowsPageantWindow` makes Pageant's Win32 calls behind `IPageantWindow`, excluded
the same way (ADR-0304).
