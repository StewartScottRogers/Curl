---
id: BL-563
title: Exchange SSH versions, frame binary packets and negotiate algorithms with KEXINIT
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-561, BL-528]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-563 — Exchange SSH versions, frame binary packets and negotiate algorithms with KEXINIT

## Goal

The SSH transport in `Curl.Protocol.Ssh.UnitLibrary` sends its identification string, reads the server's (skipping pre-banner lines), reads and writes unencrypted binary packets (RFC 4253 section 6: length, padding, payload, sequence numbers), sends a `KEXINIT` with BL-560's algorithm lists, and picks each algorithm by RFC 4253 section 7.1, failing as curl 8.21.0 fails when nothing matches.

## Context

- Conformance audit 2026-09-28, row 35 (Blocker, L+; SSH split: this framing, BL-564 key exchange, BL-565 cipher and MAC, BL-566 host key, BL-567/BL-568 user auth, BL-569 to BL-573 SFTP, BL-574/BL-577 SCP, BL-575 compression, BL-576 registration, BL-578 `-v`).
- Design: BL-560's ADR (identification string, algorithm lists, class structure). Contract: BL-561. Timeouts: BL-498's ADR.
- **BCL only.** Crypto comes from `System.Security.Cryptography`; this task needs only `RandomNumberGenerator` (padding and cookie), injected so tests are deterministic. What the BCL lacks is hand-built in `Curl.Cryptography.UnitLibrary` (BL-669's ADR; standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28): never a package, never a task blocked for a missing primitive. The `KEXINIT` lists offer only algorithms implemented so far, in BL-560's ADR order; BL-564, BL-565 and BL-678 to BL-680 each add theirs until the lists are the ADR's full lists.
- Measure with the reference curl against a local OpenSSH server through `Record-CurlExchange.ps1 -NoServer`: a server whose `KexAlgorithms` (then `Ciphers`, then `MACs`, then `HostKeyAlgorithms`) share nothing with Curl's lists, and a TCP server that sends a non-SSH banner (the HTTP mode of the script serves that); stderr and exit code.

## Acceptance criteria

- [x] Measured first as above; stderr and exit code of each copied into Notes.
- [x] `Curl.Protocol.Ssh.UnitTests` pin the identification string, packet framing (padding rules, block size, maximum length) against an in-memory peer, and the algorithm choice for matching and non-matching lists, with the measured exit code and message for each failure.
- [x] No test needs `TestCategory=Integration`; tests are platform-neutral; the library references only `Curl.Protocol.Abstractions.UnitLibrary`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

### Measured 2026-09-28

Reference: `curl 8.21.0 (x86_64-w64-mingw32) ... libssh2/1.11.1` (Windows) and
`curlimages/curl:8.21.0` (OpenSSL build, Linux), against OpenSSH_9.7p1 in an
`alpine:3.20` container (one `sshd` per restriction), through
`Record-CurlExchange.ps1 -NoServer -CurlArgs -k,-u,u:p,-m,10,sftp://127.0.0.1:<port>/etc/hostname`;
the non-SSH cases through `Record-CurlExchange.ps1 -Response <bytes> -HoldOpenMilliseconds 2000`
with `-s -S -k`.

| Case | Exit | stderr |
| --- | --- | --- |
| sshd defaults (control) | 0 | file body on stdout |
| `KexAlgorithms curve25519-sha256` (Windows) | 2 | `curl: (2) Failure establishing ssh session: -5, Unable to exchange encryption keys` |
| `KexAlgorithms sntrup761x25519-sha512@openssh.com` (Linux build) | 2 | same, `-5` |
| `Ciphers aes256-gcm@openssh.com` (Windows) | 2 | same, `-5` |
| `MACs umac-64@openssh.com` (Windows and Linux) | 2 | same, `-5` |
| `HostKeyAlgorithms ssh-ed25519` (Windows) | 2 | same, `-5` |
| HTTP response, then close | 2 | `curl: (2) Failure establishing ssh session: -13, Failed getting banner` |
| nothing, then close | 2 | same, `-13` |
| `hello`, `world`, then `SSH-2.0-OpenSSH_9.9`, no KEXINIT, then close | 2 | `curl: (2) Failure establishing ssh session: -1, Unable to exchange encryption keys` |
| `SSH-1.5-OpenSSH_1`, then close | 2 | same, `-1` (libssh2 accepts any `SSH-` line and sends its KEXINIT) |
| 310-byte identification line | 2 | same, `-1` (accepted; failed only when the peer closed) |
| identification, then `packet_length` 0x00100000 | 2 | same, `-1` |
| identification, then `packet_length` 12 with `padding_length` 2 | 2 | same, `-1` |

sshd's log confirmed each client offer, e.g. for Windows: `Their offer: diffie-hellman-group-exchange-sha256,...,ext-info-c,kex-strict-c-v00@openssh.com`,
matching ADR-0122. curl sent only `SSH-2.0-libssh2_1.11.1\r\n` (24 bytes) until a banner
arrived, then its 1088-byte KEXINIT packet (payload 1072 bytes, padding 11: libssh2 pads
minimally to 8-byte blocks with at least 4 bytes). When the connection was still open after
a failure it also sent `SSH_MSG_DISCONNECT` reason 11 `Shutdown` (payload 21, padding 6) at
teardown; that belongs to the handler's session teardown (BL-576), not this task.

### Decisions (defaults taken, within ADR-0122)

- Lower layers throw BCL exceptions (`InvalidDataException`, `EndOfStreamException`);
  `SshTransport` maps them to curl's message: every framing, truncation, early-close or
  unexpected-message failure during the KEXINIT phase is `-1` (as measured), no shared
  algorithm is `-5`, no banner is `-13`. Exit 2 throughout.
- Maximum packet size 40000 bytes (libssh2's `LIBSSH2_PACKET_MAXPAYLOAD`), above RFC 4253's
  35000; unencrypted packets must also be a multiple of 8 with at least 4 padding bytes and a
  non-empty payload.
- Identification lines are capped at 8192 bytes only to bound memory; libssh2 accepted a
  310-byte one, so the RFC's 255 is not enforced. Any `SSH-` version is accepted, as libssh2 does.
- Before the server's KEXINIT, `IGNORE`, `DEBUG` and `UNIMPLEMENTED` are skipped; any other
  message fails with `-1`. Under strict key exchange (both signals present) even those fail
  with `-1`: the server's KEXINIT must be its first packet. Not separately measurable (the
  code is the same `-1` either way).
- `SshAlgorithmCatalogue.Implemented` holds only `ext-info-c`, `kex-strict-c-v00@openssh.com`
  and `none` today, so the KEXINIT offers only those until BL-564, BL-565, BL-575 and BL-678
  to BL-680 register their algorithms; negotiation is tested with a catalogue holding every
  name. `SshNegotiatedAlgorithms.DiscardServerGuess` tells BL-564 to drop a wrongly guessed
  first key-exchange packet.
- Known-hosts narrowing (`SshAlgorithmPreferences.NarrowHostKeysTo`) and `--compressed-ssh`
  (`WithCompression`) are pure functions here; BL-566 and BL-575 feed them.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. SSH transport exchanges identification strings, frames unencrypted packets, sends the preset KEXINIT and negotiates algorithms with curl's measured exit 2 failures
