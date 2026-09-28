---
id: BL-575
title: Compress SSH packets with zlib@openssh.com when --compressed-ssh is given
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-565]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-575 — Compress SSH packets with zlib@openssh.com when --compressed-ssh is given

## Goal

With `--compressed-ssh`, the `KEXINIT` offers the compression methods curl 8.21.0's build offers (`zlib@openssh.com`, `zlib`, `none` as measured), and when one is chosen packets are compressed and decompressed with a stream that persists across packets, starting after user authentication for the `@openssh.com` form.

## Context

- Conformance audit 2026-09-28, row 31. Builds on BL-565's packet layer; the option arrives on the context (BL-561, BL-562).
- **BCL only.** `System.IO.Compression.ZLibStream` or `DeflateStream` with sync flushes per packet (SSH needs `Z_PARTIAL_FLUSH`-compatible output; check that the BCL's flush produces what OpenSSH accepts). If the BCL's flush output is not what OpenSSH accepts, hand-build the deflate stream the SSH packet layer needs in its own `Curl.Compression.UnitLibrary` with its `.UnitTests` (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28): file that as a task (a new-project task and an implementation task) and add it to this task's `depends-on`; never a package, never a task left blocked.
- Measure the offered list: `sshd -ddd` log of the reference curl's `KEXINIT` with and without `--compressed-ssh` through `Record-CurlExchange.ps1 -NoServer`.

## Acceptance criteria

- [ ] Measured first as above; the client compression lists copied into Notes.
- [ ] `Curl.Protocol.Ssh.UnitTests` pin the offered lists and round-trip compressed packets across several packets against the in-memory peer, including delayed start for `zlib@openssh.com`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
