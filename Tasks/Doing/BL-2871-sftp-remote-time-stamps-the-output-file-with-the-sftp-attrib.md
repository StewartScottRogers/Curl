---
id: BL-2871
title: SFTP --remote-time stamps the output file with the SFTP attributes' modification time (upstream case 1446)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-19
completed:
---
# BL-2871 — SFTP --remote-time stamps the output file with the SFTP attributes' modification time (upstream case 1446)

## Goal

`curl -R sftp://host/path/file` sets the saved file's last-write time to the `mtime` the SFTP server reported, as curl does, so upstream case 1446 can pass.

## Context

Found by BL-2855 (2026-10-19): `CurlCommandRunner` stamps an `-o` file under `-R` only when the transfer's `TransferResult.SourceLastWriteUnixSeconds` is set (`Curl.Console/CurlCommandRunner.cs`, near `StampOutputFileTimeAsync`). The file and HTTP handlers set it; nothing in `Curl.Protocol.Ssh.UnitLibrary` does, so an SFTP download under `--remote-time` keeps the time it was written. curl's `lib/vssh` SFTP code takes the file time from the `SSH_FXP_ATTRS` reply's `mtime` (the stat curl makes before the download, `SSH_FILEXFER_ATTR_ACMODTIME`) and the tool applies it under `-R`. The test sshd stand-in (`Curl.Conformance.SshServer.UnitLibrary`, `SshServerSftpProcess.WriteAttributes`) already sends `ACMODTIME` with the file's last-write time.

Upstream: curl 8.22.0 `tests/data/test1446` (`--remote-time` on `rofile.txt`, whose time `test613.pl prepare` sets to 978264000; the postcheck exits 1 unless the download carries it) and `lib/vssh/libssh2.c` / `lib/vssh/libssh.c`.

Decide whether SCP (`T` line with `-p`) needs the same; curl's SCP download does not ask for `-p`, so likely not - record the decision under Notes.

## Acceptance criteria

- [ ] An SFTP download's `TransferResult.SourceLastWriteUnixSeconds` is the `mtime` from the file's SFTP attributes when they carry `ACMODTIME`, and null when they do not; a focused test in `Curl.Protocol.Ssh.UnitTests` fails without the fix.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Curl.Protocol.Ssh.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: 3c77b5e9 aa792584.
