---
id: BL-595
title: Negotiate an SMB session and authenticate it with NTLM
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-594, BL-684, BL-668, BL-532]
touches: [Curl.Protocol.Smb.UnitLibrary, Curl.Protocol.Smb.UnitTests, Documentation/Planning/Decisions/ADR-0200-smb-and-smbs-speak-curls-smbv1-nt-lm-0-12-on-every-platform.md]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-595 — Negotiate an SMB session and authenticate it with NTLM

## Goal

On every platform, an `SmbProtocolHandler` connects through `IConnector`, sends the NetBIOS-framed negotiate request curl 8.21.0 sends, sets up a session with NTLM from `Curl.Ntlm.UnitLibrary` (BL-683, BL-684) as BL-594's ADR decides, and maps failures to curl's exit codes and messages (67 for a refused login).

## Context

- Conformance audit 2026-09-28, row 39. Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): SMB is offered on every platform (BL-594's ADR). Add the `Curl.Ntlm.UnitLibrary` reference (allowed once BL-668 lands) and amend `Curl.Protocol.Smb.UnitLibrary/CLAUDE.md` to name it.
- Measure off Windows with the reference OpenSSL-build curl through `Record-CurlExchange.ps1 -Script` (BL-532) or `-NoServer` against a local Samba server: negotiate and session setup succeeding and refused; record `request.bin`, stderr and exit code.

## Acceptance criteria

- [x] Measured first as above; request bytes, stderr and exit code copied into Notes.
- [x] `Curl.Protocol.Smb.UnitTests` pin the negotiate and session-setup bytes (NTLM fields from fixed inputs) and the outcome for each case through a fake connection.
- [x] Tests are platform-neutral and pass on Windows, Linux and macOS.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Smb.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- **Measured 2026-09-29** with Ubuntu's curl 8.18.0 (OpenSSL, WSL; the Windows Schannel
  build has no SMB): `Record-CurlExchange.ps1 -Script <file> -Curl wsl.exe -ListenAddress
  172.26.96.1 -Port 14450 -CurlArgs '-e','curl','-sS','-m','4','-u','User:Password',
  'smb://172.26.96.1:14450/share/x.txt'`. No Samba server was available, so the server
  side was hand-assembled (session key 0x12345678, challenge 0123456789abcdef, UID 0x64).
  - NEGOTIATE sent: `00 00 00 2f ff 53 4d 42 72 00 00 00 00 18 41 00 ba 00 00 00 00 00 00 00
    00 00 00 00 00 00 1d d7 00 00 00 00 00 0c 00 02 4e 54 20 4c 4d 20 30 2e 31 32 00`.
  - SESSION_SETUP_ANDX sent (155 bytes): header as above with command 0x73, then
    `0d ff 00 00 00 00 90 01 00 01 00 78 56 34 12 18 00 18 00 00 00 00 00 08 00 00 00 5a 00`,
    LM `98 de f7 b8 ... cd ef 13`, NT `67 c4 30 11 ... 27 84 1f 94` (MS-NLMP 4.2.2's),
    `User\0 172.26.96.1\0 x86_64-pc-linux-gnu\0 curl\0`. Full bytes, and the `DOM\Us:pw`
    variant, are in `Curl.Protocol.Smb.UnitTests/SmbRecordedExchange.cs`.
  - Accepted setup: curl goes on to TREE_CONNECT_ANDX (BL-596's).
  - Setup refused (status 0xc000006d): stderr `curl: (67) Login denied`, exit 67.
  - Negotiate refused (status 0xc0000022): stderr `curl: (7) Could not connect to server`, exit 7.
  - No `-u`: `curl: (67) Login denied`, connected but nothing sent.
  - No share in the path (`/x.txt`): `curl: (3) missing share in URL path for SMB`, no connect.
  - Server closes mid-exchange: curl polls until `-m`, `curl: (28) Operation timed out ...`.
  - The recorder writes `-1` to exitcode.txt for the two exit-67 runs; stderr shows 67.
  - First attempts hung because a hand-built response was one byte short of its NetBIOS
    length; curl waits for the rest, which is also what `SmbMessageReader` does.
- **Decisions** (ADR-0200 addendum, Decided by Claude under Stewart's delegation): the
  session setup's OS string is each platform's `curl -V` host triple; a closed connection
  waits on the transfer's cancellation, as curl waits for `-m`; `smbs` with no user fails
  67 after TLS rather than before (the connector owns TLS). The share path is parsed
  (exit 3) before connecting as curl does; BL-596 uses it for TREE_CONNECT.
- Until BL-596, an accepted session ends the transfer with exit 0 and nothing written;
  the handler is not registered until BL-598, so no user sees it.
- User/domain split is SMB's own (`/` first, then `\`), not `NtlmUserName`'s (`\` first).
- **touches** gained ADR-0200's file for the addendum: no task in Doing names it.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Protocol.Smb.UnitLibrary` reports 100%
  line, 100% branch, 41 members, 0 failing, worst CRAP 8. 48 tests in
  `Curl.Protocol.Smb.UnitTests`; `dotnet build Curl.slnx -warnaserror` clean; fast tests
  green across all 33 test projects (17171 tests).

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. smb and smbs negotiate SMBv1 and set up an NTLMv1-authenticated session with curl's bytes and exit codes
