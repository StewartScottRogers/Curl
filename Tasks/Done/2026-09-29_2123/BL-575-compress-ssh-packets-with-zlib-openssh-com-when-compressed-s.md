---
id: BL-575
title: Compress SSH packets with zlib@openssh.com when --compressed-ssh is given
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-565]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-575 — Compress SSH packets with zlib@openssh.com when --compressed-ssh is given

## Goal

With `--compressed-ssh`, the `KEXINIT` offers the compression methods curl 8.21.0's build offers (`zlib@openssh.com`, `zlib`, `none` as measured), and when one is chosen packets are compressed and decompressed with a stream that persists across packets, starting after user authentication for the `@openssh.com` form.

## Context

- Conformance audit 2026-09-28, row 31. Builds on BL-565's packet layer; the option arrives on the context (BL-561, BL-562).
- **BCL only.** `System.IO.Compression.ZLibStream` or `DeflateStream` with sync flushes per packet (SSH needs `Z_PARTIAL_FLUSH`-compatible output; check that the BCL's flush produces what OpenSSH accepts). If the BCL's flush output is not what OpenSSH accepts, hand-build the deflate stream the SSH packet layer needs in its own `Curl.Compression.UnitLibrary` with its `.UnitTests` (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28): file that as a task (a new-project task and an implementation task) and add it to this task's `depends-on`; never a package, never a task left blocked.
- Measure the offered list: `sshd -ddd` log of the reference curl's `KEXINIT` with and without `--compressed-ssh` through `Record-CurlExchange.ps1 -NoServer`.

## Acceptance criteria

- [x] Measured first as above; the client compression lists copied into Notes.
- [x] `Curl.Protocol.Ssh.UnitTests` pin the offered lists and round-trip compressed packets across several packets against the in-memory peer, including delayed start for `zlib@openssh.com`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- **Measured 2026-09-29**, Windows reference curl 8.21.0 (libssh2 1.11.1, WinCNG), `Record-CurlExchange.ps1 -Port 47575 -Response 'SSH-2.0-OpenSSH_9.9\r\n' -HoldOpenMilliseconds 2500 -CurlArgs [--compressed-ssh] -v -k -m 3 scp://127.0.0.1:47575/x`, the `KEXINIT` decoded from `request.bin` (the loopback recorder stands in for `sshd -ddd`; it captures the same client bytes):
  - without `--compressed-ssh`: compression c2s `none`, s2c `none`;
  - with `--compressed-ssh`: compression c2s `zlib,zlib@openssh.com,none`, s2c `zlib,zlib@openssh.com,none`.
  This is ADR-0122's measured list and order (it corrects the goal's `zlib@openssh.com, zlib, none`); the OpenSSL build's list is ADR-0122's same measurement.
- **BCL is enough** - no `Curl.Compression.UnitLibrary` task was needed. `ZLibStream.Flush()` is a sync flush (`...00 00 FF FF`), and `ZLibStream` inflating over a `MemoryStream` refilled per packet returns what each packet completes, so OpenSSH's `Z_PARTIAL_FLUSH` output (which can end mid-byte) inflates packet by packet. Pinned by a hand-built two-packet partial-flush vector in `SshZlibDecompressorTests`.
- **Real OpenSSH check**: OpenSSH 10 `sshd` in WSL (`~/bl575/sshd_config`, `Compression yes`, offering `none,zlib@openssh.com`) negotiated `zlib@openssh.com` both ways ("Enabling compression at level 6"); `curl.exe --compressed-ssh` downloaded a 668894-byte file over SFTP and uploaded it back byte for byte; the reference curl downloaded the same bytes.
- **Design** (ADR-0264): `Compression` folder (`SshCompressionMethods`, `SshZlibCompressor`, `SshZlibDecompressor`); `SshPacketWriter.StartCompression` / `SshPacketReader.StartDecompression`; `SshTransport` starts `zlib` at each direction's first `NEWKEYS` and `zlib@openssh.com` in `StartDelayedCompression`, which `SshUserAuthentication` calls on reading `SSH_MSG_USERAUTH_SUCCESS`. Streams start once from the first exchange's choice and last across re-exchanges, as OpenSSH keeps them. A packet may inflate to at most 40000 bytes (libssh2's `LIBSSH2_PACKET_MAXPAYLOAD`). Level: `CompressionLevel.Optimal` (zlib default, as libssh2).
- `InMemorySshServer.Compression` makes the fake server compress as OpenSSH does (`zlib` from `NEWKEYS`, `zlib@openssh.com` from its `USERAUTH_SUCCESS`); `SshProtocolHandlerTests.Compression.cs` runs multi-packet SFTP downloads and uploads and an SCP download through it, on both presets.
- **Touches**: added `Documentation/Planning/Decisions` for ADR-0264 and its index row; no task in Doing names it (BL-717 touches HTTP, Networking and Console).
- **Follow-up filed**: BL-991 - a connection reset during the SSH handshake escapes as an unhandled `IOException` (seen while setting up the real sshd).

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. --compressed-ssh offers zlib,zlib@openssh.com,none and compresses packets with a session-long ZLibStream per direction, zlib@openssh.com from USERAUTH_SUCCESS; verified against real OpenSSH
