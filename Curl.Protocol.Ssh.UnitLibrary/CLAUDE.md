# Curl.Protocol.Ssh.UnitLibrary

Phase 2.

File transfer over SSH.

**URL schemes:** `scp`, `sftp`

This library may reference `Curl.Protocol.Abstractions.UnitLibrary` and the
hand-built libraries ADR-0120 lists; it references `Curl.Cryptography.UnitLibrary`
for `FiniteFieldDiffieHellman`, `DsaSignature`, `Des`, `X25519` and `Ed25519`. Referencing another protocol
library is a build break, and `Curl.Protocol.Abstractions.UnitTests` fails if one
appears.

Folders follow ADR-0122's structure. `Transport` frames packets and runs the
handshake (`SshTransport`: identification, `KEXINIT`, key exchange, `NEWKEYS`, a
server's re-exchange); `Negotiation` holds the presets and the catalogue of
implemented names; `KeyExchange` holds one `ISshKeyExchange` per method family and
`SshKeyDerivation`; `HostKeys` holds one `ISshSignatureVerifier` per host-key type and
`KnownHostsFile` and `SshHostKeyChecker`, which accept or refuse the host key (ADR-0213);
`PacketProtection` holds one `ISshPacketProtection` per cipher family, which the packet
reader and writer switch to at each `NEWKEYS` (ADR-0212); `Authentication` holds
`SshUserAuthentication`, which requests the `ssh-userauth` service and authenticates the
user with `none`, `publickey`, `password` and `keyboard-interactive` in curl's order
(ADR-0215, ADR-0230); `Keys` holds `SshUserKeySource`, which finds `--key` (or curl's
default files through an injected `HOME` reader), `--pubkey` and `--pass`,
`SshPrivateKeyReader`, which reads every PEM, PKCS #8 and `openssh-key-v1` key file
ADR-0122 lists but Ed25519 and bcrypt (BL-681), and one `SshPrivateKey` per key type
(`RsaSshPrivateKey`, `EcdsaSshPrivateKey`, `DsaSshPrivateKey`), which signs;
`Connection` holds `SshSessionChannel`, one RFC 4254 `session` channel with libssh2's
window and packet size, which starts a subsystem or an `exec` command; `Scp` holds
`ScpFileDownload`, which runs `scp -pf` (`ScpCommand`, `ScpRemotePath`), reads its `T`
and `C` lines with libssh2's checks (`ScpFileHeaderReader`, `ScpHeaderNumber`) and copies
the file (ADR-0225); `Sftp` holds `SftpSession`, the SFTP version 3 client over that
channel, and `SftpFileDownload`, which downloads one file with `SftpReadAhead`'s reads in
flight and maps each `SSH_FX_*` status to curl's exit code through `SftpStatusCode`
(ADR-0220). A new algorithm registers in
`SshKeyExchangeMethods`, `SshSignatureVerifiers` or `SshPacketProtections`, and
`SshAlgorithmCatalogue.Implemented` offers it from then on. Failure messages and
their libssh2 codes are ADR-0122's, ADR-0206's and ADR-0212's, measured from the
reference builds.

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnection`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network.
