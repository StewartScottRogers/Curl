---
id: BL-596
title: Connect to an SMB share and download a file
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-595]
touches: [Curl.Protocol.Smb.UnitLibrary, Curl.Protocol.Smb.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-596 — Connect to an SMB share and download a file

## Goal

`smb://host/share/path` connects to the share (tree connect), opens the file, reads it and writes its bytes, with a missing share or file mapped to curl 8.21.0's exit code and message.

## Context

- Conformance audit 2026-09-28, row 39. Builds on BL-595; dialect and messages: BL-594's ADR.
- Measure against a local Samba server as in BL-595: a file, a missing file, a missing share, a directory.

## Acceptance criteria

- [x] Measured first as above; stdout bytes, stderr and exit code copied into Notes.
- [x] `Curl.Protocol.Smb.UnitTests` pin the tree-connect, open and read requests and the output and outcome for each case.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Smb.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- **Measured 2026-09-29** as BL-595 did: no Samba server is available and the Windows
  Schannel build has no SMB, so Ubuntu's curl 8.18.0 (OpenSSL, WSL) ran against
  `Record-CurlExchange.ps1 -Script <file> -Curl wsl.exe -ListenAddress 172.26.96.1
  -Port 14450 -CurlArgs '-e','curl','-sS','-m','6','-u','User:Password',
  'smb://172.26.96.1:14450/share/dir/x.txt'`, with the server side hand-assembled from
  `lib/smb.c` and MS-CIFS (TID 0x0007, FID 0x4001). Every request byte is in
  `SmbRecordedExchange`.
  - File (open: size 11; read: `hello world`): stdout `hello world`, stderr empty, exit 0.
    curl sends TREE_CONNECT_ANDX (`\\172.26.96.1\share` + `?????`), NT_CREATE_ANDX
    (`dir\x.txt`, GENERIC_READ, share all, FILE_OPEN), READ_ANDX (0x8000 at 0), CLOSE,
    TREE_DISCONNECT.
  - Missing file (open status 0xc0000034): stdout empty, `curl: (78) Remote file not found`,
    exit 78; curl sends TREE_DISCONNECT after the open, no CLOSE.
  - Missing share (tree connect status 0xc00000cc): `curl: (78) Remote file not found`,
    exit 78; nothing sent after the tree connect.
  - Share refused with DOS ERRnoaccess (0x00050001): `curl: (9) Access denied to remote
    resource`, exit 9.
  - Directory (open succeeds, size 0, directory attribute; read status 0xc0000010):
    stdout empty, `curl: (56) Failure when receiving data from the peer`, exit 56;
    curl still sends CLOSE and TREE_DISCONNECT.
- **From `lib/smb.c` at `curl-8_21_0`, not measured:** a read returning 0x8000 bytes reads
  again at the new offset (64-bit, split low/high); a negative size is exit 8 then CLOSE;
  data past the frame is exit 56 `Invalid input packet`; tree connect or open bytes past
  1024 are exit 63 with nothing sent; a frame the reader refuses ends at once without
  CLOSE or TREE_DISCONNECT; close and disconnect statuses are ignored; `-R` takes the
  open response's last change time (`get_posix_time`: before 1970 is 1970).
- **Defaults taken:** the tree connect sends the URL's IDN host (curl's
  `conn->host.name`); a failed output write is exit 23 with the gopher handler's
  `passed N returned M` text, then CLOSE and TREE_DISCONNECT as curl does for a
  `Curl_client_write` error. `SmbMessages.SetupTooLarge` became `MessageTooLarge`, since
  exit 63 now covers three messages.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Protocol.Smb.UnitLibrary` reports 100%
  line, 100% branch, 68 members, 0 failing, worst CRAP 10. 75 tests in
  `Curl.Protocol.Smb.UnitTests`; `dotnet build Curl.slnx -warnaserror` clean; fast tests
  green in all 33 test projects.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. smb:// connects to the share, opens and reads the file to the output, and maps a missing share or file to curl's exit 78, ERRnoaccess to 9 and a directory to 56
