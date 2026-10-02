---
id: BL-1123
title: Report curl's SFTP quote and create-directory -v lines
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1123 — Report curl's SFTP quote and create-directory -v lines

## Goal

Under `-v`, an `sftp://` transfer reports the two info lines curl 8.21.0's libssh2 back end prints and Curl does not: `SSH: sending quote commands` before the `-Q` commands that run before the transfer and again before those that run after it (`-Q -cmd`), and `SFTP: creating directory '<path>'` before each `MKDIR` that `--ftp-create-dirs` sends.

## Context

- curl 8.21.0, `lib/vssh/libssh2.c` (https://github.com/curl/curl/blob/curl-8_21_0/lib/vssh/libssh2.c):
  - `ssh_state_sftp_quote_init` (line 1900): `if(data->set.quote) { infof(data, "SSH: sending quote commands"); ... }`; `ssh_state_sftp_postquote_init` (line 1914): the same line when `data->set.postquote` is set. No line is printed when the list is empty.
  - The create-directories state (line 2729): for each `/` after the first character of the path, the path is cut there and `infof(data, "SFTP: creating directory '%s'", sshp->path)` is printed before the `MKDIR`, so `/a/b/c.txt` prints `SFTP: creating directory '/a'` and then `SFTP: creating directory '/a/b'`.
- Curl today: `Curl.Protocol.Ssh.UnitLibrary/Sftp/SftpQuoteCommands.cs` `RunBeforeTransferAsync` and `FinishAsync` (through `RunAfterTransferAsync`) run the commands without that line; `Sftp/SftpFileUpload.cs` `CreateDirectoriesAndOpenAsync` / `MakeDirectoryAsync` send one `MKDIR` per directory without a line. Other `SSH:` lines live in `SshInfoLines.cs`; add these two there and report them through the transfer's `ITransferEvents` (pass it in where it is not yet available).
- The path printed is the path the `MKDIR` carries (after `/~/` is resolved to the home directory), shown as UTF-8 text as `SftpQuoteCommands.Show` does.

## Acceptance criteria

- [ ] New tests in `Curl.Protocol.Ssh.UnitTests` pin `SSH: sending quote commands` once before the first pre-transfer `-Q` command is sent, once before the first post-transfer one, and not at all when there are no commands of that kind.
- [ ] A test pins, for an upload to `/a/b/c.txt` under `--ftp-create-dirs` whose first open fails with status 2, the lines `SFTP: creating directory '/a'` and `SFTP: creating directory '/a/b'`, each reported before its `MKDIR` request is written.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
