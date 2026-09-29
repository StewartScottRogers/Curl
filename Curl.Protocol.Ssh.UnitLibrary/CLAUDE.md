# Curl.Protocol.Ssh.UnitLibrary

Phase 2.

File transfer over SSH.

**URL schemes:** `scp`, `sftp`

This library may reference `Curl.Protocol.Abstractions.UnitLibrary` and the
hand-built libraries ADR-0120 lists; it references `Curl.Cryptography.UnitLibrary`
for `FiniteFieldDiffieHellman`, `DsaSignature`, `X25519` and `Ed25519`. Referencing another protocol
library is a build break, and `Curl.Protocol.Abstractions.UnitTests` fails if one
appears.

Folders follow ADR-0122's structure. `Transport` frames packets and runs the
handshake (`SshTransport`: identification, `KEXINIT`, key exchange, `NEWKEYS`, a
server's re-exchange); `Negotiation` holds the presets and the catalogue of
implemented names; `KeyExchange` holds one `ISshKeyExchange` per method family and
`SshKeyDerivation`; `HostKeys` holds one `ISshSignatureVerifier` per host-key type and
`KnownHostsFile` and `SshHostKeyChecker`, which accept or refuse the host key (ADR-0213);
`PacketProtection` holds one `ISshPacketProtection` per cipher family, which the packet
reader and writer switch to at each `NEWKEYS` (ADR-0212). A new algorithm registers in
`SshKeyExchangeMethods`, `SshSignatureVerifiers` or `SshPacketProtections`, and
`SshAlgorithmCatalogue.Implemented` offers it from then on. Failure messages and
their libssh2 codes are ADR-0122's, ADR-0206's and ADR-0212's, measured from the
reference builds.

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnection`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network.
