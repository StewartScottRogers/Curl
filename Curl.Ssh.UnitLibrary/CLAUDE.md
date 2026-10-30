# Curl.Ssh.UnitLibrary

The SSH (RFC 4253) client transport, shared by `Curl.Protocol.Ssh.UnitLibrary`, the conformance
SSH server and extensions (ADR-0999 lets an extension reference a shared layer, never a protocol
library; ADR-1165): `Transport` (`SshTransport`, the packet reader and writer, `SshTransportLog`),
`Negotiation`, `KeyExchange`, `PacketProtection`, `Compression`, the host-key signature verifiers
in `HostKeys`, and at the root `ISshRandomSource`, `SystemSshRandomSource`,
`SshConnectionLostException`, `Libssh2ErrorCode` and `SshTransferException`.

This library references only `Curl.Cryptography.UnitLibrary` and
`Curl.Protocol.Abstractions.UnitLibrary` (ADR-0120's table). curl's SSH options, `known_hosts`,
user authentication, channels, SCP and SFTP stay in `Curl.Protocol.Ssh.UnitLibrary`, whose
`CLAUDE.md` describes each folder of this one. Every type is `internal`; a new consumer is added
to the `InternalsVisibleTo` list in the project file. A new algorithm registers in
`SshKeyExchangeMethods`, `SshSignatureVerifiers`, `SshPacketProtections` or
`SshCompressionMethods`, and `SshAlgorithmCatalogue.Implemented` offers it from then on.
