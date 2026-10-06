---
id: BL-564
title: Run the SSH key exchange and derive the session keys
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-563, BL-739, BL-745]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-564 — Run the SSH key exchange and derive the session keys

## Goal

The transport runs every BCL-backed key exchange BL-560's ADR offers (ECDH on NIST curves per RFC 5656; finite-field `diffie-hellman-group1-sha1`, `group14-sha1`, `group14-sha256`, `group16-sha512`, `group18-sha512` per RFC 4253 and RFC 8268; `diffie-hellman-group-exchange-sha1`/`sha256` per RFC 4419; `curve25519-sha256` is BL-678), verifying `rsa-sha2-256`, `rsa-sha2-512`, `ssh-rsa`, `ssh-dss` and `ecdsa-sha2-nistp*` host-key signatures (`ssh-ed25519` is BL-678), computes the exchange hash and session identifier, verifies the server's signature over it with the host-key algorithm chosen, derives the six keys (RFC 4253 section 7.2), and switches keys on `NEWKEYS`.

## Context

- Conformance audit 2026-09-28, row 35. Builds on BL-563.
- **BCL only.** `ECDiffieHellman`, `System.Numerics.BigInteger`, `SHA1`/`SHA256`/`SHA384`/`SHA512`, `RSA`, `DSA` and `ECDsa` for signature checks. Whether the server's host key is *trusted* is BL-566; this task checks the signature only. What the BCL lacks (on any CI platform, per BL-669's ADR) is hand-built in `Curl.Cryptography.UnitLibrary` (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28): never a package, never a task blocked for a missing primitive.
- Test vectors: fixed client ephemeral keys (injected), a test peer with fixed host keys; cross-check one exchange hash against a value computed independently in the test from the RFC's definition.
- Measure: a server whose signature is bad cannot be staged with OpenSSH; pin curl's exit code for a failed key exchange from BL-563's measurements (a server that closes during KEX) and record it in Notes.

## Acceptance criteria

- [x] `Curl.Protocol.Ssh.UnitTests` complete a key exchange for each offered method against the in-memory peer, pin the exchange hash and derived keys for fixed inputs, and reject a bad signature and a bad group element with the exit code and message recorded in Notes.
- [x] Re-keying requested by the server (`KEXINIT` mid-session) is handled or refused as BL-560's ADR states, with a test.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

### Measured 2026-09-29

Reference: `curl 8.21.0 (x86_64-w64-mingw32) ... libssh2/1.11.1` (WinCNG), through
`Record-CurlExchange.ps1 -Response <bytes> -HoldOpenMilliseconds 3000 -CurlArgs -v,-k,-u,u:p,-m,10,sftp://127.0.0.1:<port>/x`.
The response was built by a throwaway PowerShell script: `SSH-2.0-OpenSSH_9.7\r\n`, a
`KEXINIT` offering `diffie-hellman-group14-sha256`, `rsa-sha2-256`, `aes128-ctr`,
`hmac-sha2-256`, `none`, then a `KEXDH_REPLY` with a fresh 2048-bit RSA host key.

| Case | Exit | stderr |
| --- | --- | --- |
| all-zero RSA signature (bad signature) | 2 | `curl: (2) Failure establishing ssh session: -8, Unable to exchange encryption keys` |
| f = 1 (bad group element) | 2 | same |
| f = 0 (bad group element) | 2 | same |
| signature blob names `rsa-sha2-512` | 2 | same |
| no reply, server closes during KEX | 2 | same |

`SSH_MSG_KEX_DH_GEX_REQUEST` for `diffie-hellman-group-exchange-sha256` and `-sha1`:
min 2048, n 4096, max 4096 (payload `22 00000800 00001000 00001000`). The OpenSSL build
could not be measured (Docker engine down): filed as BL-888.

### Decisions (ADR-0206)

- Every failure after the algorithms are agreed maps to `-8, Unable to exchange
  encryption keys`, exit 2 (`Libssh2ErrorCode.KeyExchangeMethodFailure`).
- Group exchange asks for (2048, 4096, 4096) on every preset and refuses a prime outside
  2048..4096 bits or a group `FiniteFieldDiffieHellmanGroup.TryCreate` refuses.
- NIST points are validated by hand in `SshNistCurve` (length, `0x04`, x and y below p,
  on the curve), not left to the platform's import.
- Re-keying is handled: `SshTransport.ReExchangeKeysAsync(serverKexInitPayload)` answers
  with the client's `KEXINIT`, renegotiates, runs a new exchange and keeps the first H as
  the session identifier. Strict key exchange binds only the first exchange's message
  order; sequence numbers reset at every `NEWKEYS` once it was agreed.
- Ephemeral keys come from a new internal `ISshEphemeralKeySource`
  (`SystemSshEphemeralKeySource` in production), not `ISshRandomSource` as ADR-0122
  sketched: the BCL's `ECDiffieHellman` draws its own private key.
- "Switches keys on `NEWKEYS`" here means: `NEWKEYS` sent and received, strict sequence
  numbers reset, and an `SshKeyDerivation` handed out for the six keys at whatever length
  each cipher/MAC needs. Installing packet protection is BL-565.
- `diffie-hellman-group1-sha1` uses Oakley group 2 (1024-bit), as RFC 4253 section 8.1
  defines it; `FiniteFieldDiffieHellmanGroup.Group1` (768-bit) is not SSH's group 1.
- `SshAlgorithmCatalogue.Implemented` now offers these key exchanges and host keys; the
  KEXINIT still fails every server with `-5` until BL-565 adds ciphers and MACs.

### Scope

- `touches` gained `Documentation/Planning/Decisions` for ADR-0206 and its index row; no
  task in Doing named it.
- `Curl.Protocol.Ssh.UnitLibrary.csproj` references `Curl.Cryptography.UnitLibrary`
  (ADR-0120 allows it; `ProtocolIsolationTests` passes).
- Tests: the in-memory peer is `Fakes/TestKeyExchangeServer`, which computes K, H and the
  derived keys from the RFCs with its own encoder (`SshTestEncoding`); every method is
  checked against it, and the P-256 (H and the six keys) and group14/`ssh-dss` (H) cases
  are pinned.
- The final fast run passed everywhere. One earlier run failed `Curl.Networking.UnitTests`'
  `AuthenticateAsClientAsync_WithCertStatusAndARevokedStapledResponse_FailsWithExit91AndTheReason`
  intermittently; that flake is BL-881's subject and outside this task.

### Lane 1 integration, 2026-09-29

- Cherry-picked 9e19c6f4 and 5d5ae996 from `factory/BL-564-lane-6-20260929-023709` onto
  `factory/lane-1`. ADR-0204 had since been taken by the Kerberos KDC-proxy ADR, so the SSH
  ADR is now ADR-0206 (ADR-0205 went to BL-821's TLS ClientHello in the rebase) (file, heading, index row and the Ssh `CLAUDE.md` reference); the
  follow-up task is BL-888 (BL-887 was taken too).
- `dotnet build Curl.slnx -warnaserror` clean; every fast test passes (Ssh 174);
  `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary`: 100% line and branch,
  0 failing members, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Lane 6 could not integrate: fast tests failed after rebasing onto the other lanes' work. The work is on branch factory/BL-564-lane-6-20260929-023709; start with git cherry-pick --no-commit factory/BL-564-lane-6-20260929-023709 and fix it.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Overlaps Documentation/Planning/Decisions with BL-883 in Doing; code is green on factory/BL-564-lane-6-20260929-023709 - cherry-pick 9e19c6f4 5d5ae996 per Notes
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. SSH transport runs every BCL-backed key exchange, verifies host-key signatures, derives the six keys and exchanges NEWKEYS
