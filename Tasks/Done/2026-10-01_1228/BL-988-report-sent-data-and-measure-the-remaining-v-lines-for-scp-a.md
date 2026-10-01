---
id: BL-988
title: Report sent data and measure the remaining -v lines for scp and sftp uploads
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-578]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-10-01
---
# BL-988 — Report sent data and measure the remaining -v lines for scp and sftp uploads

## Goal

`-v` and `--trace-ascii -` on a `-T` upload over `scp://` or `sftp://` write curl 8.21.0's `} [N bytes data]` and `=> Send data` output and its closing lines, and the `-v` cases ADR-0262 could not measure match curl.

## Context

- BL-578 (ADR-0262) made `SshProtocolHandler` report curl's session lines and received data; uploads report no sent data yet.
- Unmeasured in ADR-0262: a right password and `keyboard-interactive` succeeding (an unprivileged `sshd` cannot check passwords; lines taken from `lib/vssh/libssh2.c`), a short file's `PartialFile` outcome (treated as leaving the connection intact), and a connection lost during `publickey`.
- Measure with `Record-CurlExchange.ps1 -NoServer` against BL-569's unpacked OpenSSH 10.2 in WSL; a password needs an `sshd` that can read the password database, or a scripted server.

## Acceptance criteria

- [x] Measured first: `-v` and `--trace-ascii -` for an `sftp` and an `scp` upload, a right password, `keyboard-interactive`, and a short `scp` file; output copied into Notes.
- [x] `Curl.Console.UnitTests` pin the measured upload `-v` stderr and trace.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- **Measurement setup.** Reference curl 8.21.0 (libssh2 1.11.1, WinCNG) through
  `Record-CurlExchange.ps1 -NoServer`, `HOME` an empty directory, against BL-569's unpacked
  OpenSSH 10.2 in WSL: the unprivileged `sshd` of BL-578 on port 2278 (it needed
  `LD_LIBRARY_PATH` for the unpacked `libwrap`) for key uploads; and, for what an
  unprivileged `sshd` cannot check, `sshd` run as root (`wsl -u root`) with a throwaway user
  `bl988` and password: port 22981 `PasswordAuthentication yes`, 22982 `UsePAM yes`
  `KbdInteractiveAuthentication yes` (password off), 22983 with a `ForceCommand` script
  answering `scp -pf` with a `T` line, `C0644 10 x` and 5 bytes. The user, the `sshd`
  privilege-separation user and the servers were removed afterwards.
- **Measured `-v`, `sftp` upload** (`-v -sS -k --key k --pubkey k.pub -u stewart_rogers: -T up.txt`, 13 bytes), exit 0:
  ```
  * SSH: authentication complete
  } [13 bytes data]
  * upload completely sent off: 13 bytes
  * Connection #0 to host 127.0.0.1:2278 left intact
  ```
  `scp` the same with `* SSH: connection established` after `authentication complete`.
- **Measured `--trace-ascii -`**, either scheme: `=> Send data, 13 bytes (0xd)`, `0000: hello
  upload.`, then `* upload completely sent off: 13 bytes` and `left intact`. 200000 bytes:
  `sftp` blocks 30000, 30000, 5536 per 64 KiB, then 3392; `scp` 32700, 32700, 136 per 64 KiB,
  then 3392. An empty source: `* Request completely sent off`. Upload to a missing
  directory: `sftp` `* Upload failed: No such file or directory (2/-31)` / 78, `scp` `* failed
  to send file` / 25, both `left intact`.
- **Measured, a right password** (`scp` upload, port 22981): `* SSH: trying private key file
  '<missing>'`, `* SSH: publickey authentication denied: Reason unknown (-1)`, `* SSH:
  initialized password authentication`, `* SSH: authentication complete`, `* SSH: connection
  established`, `} [13 bytes data]`, ... exit 0.
- **Measured, `keyboard-interactive`** (port 22982, methods `publickey,keyboard-interactive`):
  `... denied: Reason unknown (-1)`, `* SSH: trying publickey authentication via agent`, `*
  SSH: failure connecting to agent`, `* SSH: initialized keyboard interactive
  authentication`, `* SSH: authentication complete`, then the transfer; exit 0. (`sftp` to
  the root `sshd` timed out in its subsystem, so `keyboard-interactive` and the password were
  measured over `scp`; the authentication lines do not depend on the scheme.)
- **Measured, a short `scp` file** (announces 10, sends 5), exit 18:
  ```
  * SSH: connection established
  { [5 bytes data]
  * end of response with 5 bytes missing
  * closing connection #0
  curl: (18) end of response with 5 bytes missing
  ```
  `--trace-ascii -` shows `<= Recv data, 5 bytes (0x5)`, `0000: 01234`, the bytes, then `<=
  Recv data, 0 bytes (0x0)` before the failure line. A size of -1 read to the channel's end
  also shows the 0-byte block, then `left intact`. The connection killed after 5 of 10 bytes:
  `{ [5 bytes data]`, `* closing connection #0`, `curl: (79) Error in the SSH layer`.
  So ADR-0262's guess (a short file leaves the connection intact) was wrong.
- **Design (ADR-0294).** `SftpFileUpload` and `ScpFileUpload` take `ITransferEvents`: each
  `WRITE` chunk, and each `scp` channel write of at most 32700 bytes
  (`ScpFileUpload.ChannelWriteSize`), is reported as sent data, and a finished copy writes
  `SshInfoLines.UploadSent`. `SshProtocolHandler.FailedWhileTransferring` closes the
  connection after a short file or a copy's `Error in the SSH layer` (curl's `PERFORMING`
  "Transfer returned error"), not after an `sftp` listing's failure (read in curl's `DO`
  state machine). `ScpFileDownload` writes the channel's end on as an empty block.
- **Tests.** `Curl.Protocol.Ssh.UnitTests`: handler verbose tests for both uploads, empty
  uploads, block sizes, `keyboard-interactive`, the short file, a reset during the bytes and
  `FailedWhileTransferring`; `InMemorySshServer` gained `OffersKeyboardInteractive` and
  `ScpFileShortBy`. `Curl.Console.UnitTests/CurlCompositionSshVerboseTests` pins the measured
  upload `-v` and trace for both schemes, `keyboard-interactive`, and the short file's `-v`
  and trace (the `keyboard-interactive` test points `SSH_AUTH_SOCK` at nothing for its run so
  the result does not depend on the machine's agent). The built `Curl.Console` matched the
  real `sshd` for the `sftp` key upload, the `keyboard-interactive` `scp` upload and the short
  file. SSH library: 1453 tests, 100% line, 100% branch, 0 failing members.
- A connection lost during `publickey` (in Context, not in the criteria) was not measured: OpenSSH cannot be made to drop the connection at that step without a scripted SSH server; it stays as ADR-0262 decision 5 has it.
- Edited ADR-0262's status line (outside `touches`) to point at ADR-0294, since its short-file
  guess and its "uploads report no data" consequence are no longer true.
- `SystemSshAgentConnectorTests.ConnectAsync_WindowsPipeServed_ConnectsToIt` (Integration, not
  in the fast run) hung once in a full `dotnet test` of the SSH tests; unrelated to this change
  and not investigated here.

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. -v and --trace-ascii on scp and sftp uploads write curl's sent data and upload-sent lines, and a short or broken scp download closes the connection, as measured
