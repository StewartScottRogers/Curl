# ADR-0456 — The upstream harness's SSH server lives in its own library and reuses the SSH client's code

- Status: Accepted
- Date: 2026-10-09
- Decided by Claude under Stewart's delegation (BL-1899)

## Context

42 upstream SCP and SFTP cases (`%SSHPORT`, `%USER`, `%SFTP_PWD`, `%SCP_PWD`) are skipped by
the case runner in `Curl.Conformance.UnitLibrary` because it has no stand-in for upstream's
OpenSSH `sshd`. The runner's servers are in-memory `IConnector`s written with the BCL only.

`Curl.Protocol.Ssh.UnitLibrary` already holds everything an SSH transport needs on both
sides of the wire: `SshPacketReader` and `SshPacketWriter`, `SshKexInit` and the algorithm
negotiator, `X25519SshKeyShare` and `SshKeyDerivation`, `SshExchangeHashInput`, and every
packet protection (`AesCtrSshCipher`, `BclSshHmac`, `AesGcmPacketProtection`,
`ChaCha20Poly1305PacketProtection`). `Curl.Protocol.Ssh.UnitTests` already drives the client
against a working server-side session (`Fakes.InMemorySshServerSession`), but that code is
test code and is held to no coverage gate. All of the library's types are `internal`.

## Decision

1. The server goes in a new library, `Curl.Conformance.SshServer.UnitLibrary`, with its
   `Curl.Conformance.SshServer.UnitTests` twin, held to the same gates as every
   `*.UnitLibrary` (100% line and branch coverage, complexity at most 10).
2. It references `Curl.Protocol.Ssh.UnitLibrary` (and `Curl.Cryptography.UnitLibrary`) and
   reuses the client's packet, negotiation, key exchange and packet protection code rather
   than writing a second copy. `Curl.Protocol.Ssh.UnitLibrary` grants it
   `InternalsVisibleTo`. This is a test-harness library, not a protocol library, so the rule
   that protocol libraries never reference each other is untouched.
3. `Curl.Conformance.UnitLibrary` will reference `Curl.Conformance.SshServer.UnitLibrary`
   when the runner starts answering `%SSHPORT`; it still references no protocol library
   directly.
4. The host key is fixed: RFC 8032 section 7.1's first Ed25519 key as an `ssh-ed25519` host
   key (the key the SSH tests already use, `TestHostKey.Ed25519`), so its MD5 and SHA-256
   fingerprints are the same on every run and platform, and `--hostpubmd5` and
   `--hostpubsha256` cases can name them as constants on the server.
5. The work is split for the board: the transport up to `NEWKEYS` and the service request
   first, authentication and the SCP and SFTP subsystems later.

## Consequences

- One copy of the SSH wire code serves both the client and the harness's server; a fix to
  either side is a fix to both.
- `Curl.Protocol.Ssh.UnitLibrary`'s internals become visible to one more assembly.
- The harness can reproduce upstream's sshd only as far as Curl's own client code reaches;
  an algorithm the client does not implement the server cannot offer either.
