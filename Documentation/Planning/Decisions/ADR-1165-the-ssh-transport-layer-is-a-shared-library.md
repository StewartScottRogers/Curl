# ADR-1165 — The SSH transport layer is a shared library

- **Status:** Accepted
- **Date:** 2026-10-30

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4362
(split from BL-4328, with BL-4363, BL-4364 and BL-4365).

Amends ADR-0120: its table gains `Curl.Ssh.UnitLibrary`.

Amended later (BL-4402, AF-0492): more of the SSH layer moved into `Curl.Ssh.UnitLibrary` after
this decision (`known_hosts`, key files, user authentication, the session channel); the bullets
below say where they are superseded.

## Context

`--ssh-jump` (BL-4145) is an extension, and an extension references no `Curl.Protocol.*` library
but `Curl.Protocol.Abstractions` (ADR-0999). It needs an SSH client transport: the binary packet
protocol, key exchange, packet protection, compression, algorithm negotiation and host key
signature verification (RFC 4253). All of that lived in `Curl.Original.Protocol.Ssh.UnitLibrary`, beside
the SCP and SFTP layers, the user authentication, the private key files and `known_hosts`.

## Decision

- `Curl.Ssh.UnitLibrary` (solution folder `/Wire/Ssh/`) holds the SSH transport layer, each
  folder in namespace `Curl.Ssh.<Folder>`:
  - the root types `ISshRandomSource`, `SystemSshRandomSource`, `SshConnectionLostException`,
    `Libssh2ErrorCode` and `SshTransferException` (namespace `Curl.Ssh`);
  - `Transport`, `KeyExchange`, `PacketProtection`, `Compression` and `Negotiation`, whole;
  - from `HostKeys`, the host key signature verifiers (RFC 4253 section 8):
    `ISshSignatureVerifier`, `SshSignatureVerifiers`, the DSA, ECDSA, Ed25519, RSA,
    OpenSSH certificate and security key verifiers, `SshKeyBlobReader` and
    `SecurityKeySignedData`.
  It references `Curl.Cryptography.UnitLibrary` and `Curl.Protocol.Abstractions.UnitLibrary`
  only, and joins ADR-0120's table of hand-built libraries a protocol library may reference.
- `SshDiagnosticLog` is split. `Curl.Ssh.Transport.SshTransportLog` writes the transport's
  lines (the server, the algorithms, the key exchange, the host key, each message number), and
  `SshTransport` takes only it. `SshDiagnosticLog` stays in the protocol library with the SFTP
  and `known_hosts` lines, and writes through the `SshTransportLog` it wraps, so the log lines
  and their order do not change.
- *Superseded (BL-4402, AF-0492):* `known_hosts` (`KnownHostsFile`, `SshHostKeyChecker` and the
  rest of `HostKeys`), the private key files and user authentication were first to stay in the
  protocol library. They now live in `Curl.Ssh.UnitLibrary` (`HostKeys/`, `Keys/`,
  `Authentication/`), and the session channel with them (ADR-1166). Only SCP, SFTP,
  `SshDiagnosticLog`, `SshProtocolHandler` and the like stay in `Curl.Original.Protocol.Ssh.UnitLibrary`.
- `SshWireDecoders` stays in `Curl.Original.Protocol.Ssh`. It is the public entry point the fuzzer
  calls (ADR-0394), and it also decodes SFTP attributes, which are protocol-library types; moving
  it would split one fuzz surface across two libraries and break the fuzzer's `using` line, which
  a lane cannot change.
- Every moved type stays `internal`, except `ISshRandomSource`, `SystemSshRandomSource` and
  `SshAlgorithmPreferences`, which are public; `Curl.Ssh.UnitLibrary` grants `InternalsVisibleTo` to the
  protocol library and its unit and integration tests, the conformance SSH server and its tests,
  `Curl.Conformance.UnitProgram.UnitLibrary` and `Curl.Ssh.UnitTests`. An extension that uses the
  layer adds itself to that list.
- The tests move with their types to `Curl.Ssh.UnitTests`. The fakes the protocol tests share
  (`Fakes/`, `SshAuthenticationDiagnostics`, `SshByteDiagnostics`) live there and are linked into
  `Curl.Original.Protocol.Ssh.UnitTests`.

## Consequences

- An extension can open an SSH transport without referencing the SSH protocol library.
- curl's SSH behaviour, exit codes and verbose output do not change; only namespaces move.

## Alternatives considered

- **Keep the namespace `Curl.Original.Protocol.Ssh.*` in the new library.** Breaks the rule that a
  namespace matches its folder.
- **Move `SshDiagnosticLog` whole.** It names `SftpPacketType` and `KnownHostsFile`, so the
  shared layer would need the protocol library.
- **Move the `known_hosts` checks too.** They read curl's `--hostpubmd5`, `--hostpubsha256` and
  `known_hosts` options; a jump host is checked by the extension that dials it.
