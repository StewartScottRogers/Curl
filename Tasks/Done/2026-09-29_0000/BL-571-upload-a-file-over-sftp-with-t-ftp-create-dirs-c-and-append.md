---
id: BL-571
title: Upload a file over SFTP with -T, --ftp-create-dirs, -C and --append
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-569]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-571 — Upload a file over SFTP with -T, --ftp-create-dirs, -C and --append

## Goal

`-T file sftp://host/path` uploads with `OPEN` (create, truncate, curl's permission bits from `--create-file-mode`)/`WRITE`/`CLOSE`; `--ftp-create-dirs` creates missing directories (with `--create-dirs` mode), `-C <n>`/`-C -` resume at the offset or the remote size, and `-a` appends, each as curl 8.21.0 does, with failures mapped to curl's exit codes.

## Context

- Conformance audit 2026-09-28, row 35. Builds on BL-569. The existing context members (`Upload`, `ResumeFrom`, `FtpCreateDirectories`, `CreateFileMode`) are reused; `-a`/`--append` parsing is the FTP ASCII-and-append task (row 24) and may land later: if it has not, leave append to that task and note it here.
- **BCL first.** Anything the BCL lacks is hand-built in its own library (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28): never a package, never a task blocked for a missing primitive.
- Measure with the reference curl against a local OpenSSH server through `Record-CurlExchange.ps1 -NoServer`: a new file, an existing file, a missing directory with and without `--ftp-create-dirs`, `-C -` against a shorter remote file, `-T -` from standard input, and a read-only target.

## Acceptance criteria

- [x] Measured first as above; stderr, exit code and the resulting remote file (size and mode) copied into Notes.
- [x] `Curl.Protocol.Ssh.UnitTests` pin the SFTP requests (flags, attributes, offsets) and outcome for each case against the in-memory peer; `%{size_upload}` and upload progress match the measured values.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- **Measurement setup.** BL-569's WSL OpenSSH 10.2 (`sshd -D`, public-key auth) on port
  2231 with OpenSSH's `sftp-server -l DEBUG3` behind a wrapper logging to a file (every
  request with its flags, mode, offset and length), and on port 2232 BL-570's scripted
  SFTP subsystem (throwaway C# app in `%TEMP%\bl571\fake`, not committed) extended to
  answer `WRITE` and `MKDIR` by path. The reference curl 8.21.0 (libssh2 1.11.1, Schannel)
  ran through `Record-CurlExchange.ps1 -NoServer` as `curl -sS -k --key <rsa> --pubkey <pub>
  -u <user>: -T <file> -w '[%{size_upload}]' sftp://localhost:<port>/<path>`; `-T -` was
  piped through `cmd /c type`. Source: `hello upload\n` (13 bytes) unless stated.
- **Measured against `sftp-server`** (stdout / stderr / exit; remote file afterwards):
  - new file: `[13]` / empty / 0; `OPEN flags WRITE,CREATE,TRUNCATE mode 0100644`, `WRITE off 0 len 13`; 13 bytes `-rw-r--r--`
  - existing 26-byte file, mode 0600: `[13]` / empty / 0; 13 bytes, still `-rw-------`
  - `--create-file-mode 0600`: `[13]` / empty / 0; `mode 0100600`; `-rw-------`
  - missing directory: `[0]` / `curl: (78) Upload failed: No such file or directory (2/-31)` / 78; nothing created
  - missing `d1/d2` with `--ftp-create-dirs`: `[13]` / empty / 0; `OPEN` answered 2, `MKDIR "/home" mode 0755` ... `/home/stewart_rogers/bl571/files` answered 4, `d1` and `d2` made (`drwxr-xr-x`), `OPEN` again, `WRITE`, `CLOSE`; same with `d1` existing
  - read-only file (0444): `[0]` / `curl: (9) Upload failed: Permission denied (3/-31)` / 9; unchanged
  - `-C -` against a 5-byte remote: `[8]` / empty / 0; `STAT`, `OPEN flags WRITE`, `WRITE off 5 len 8`; 13 bytes
  - `-C -` against a 13- or 20-byte remote: `[0]` / empty / 0; `STAT`, `OPEN flags WRITE`, `CLOSE`; unchanged
  - `-C -` against a missing remote: `[13]` / empty / 0; `STAT` answered 2, `OPEN WRITE,CREATE,TRUNCATE`
  - `-C 5` against a 25-byte remote: `[8]` / empty / 0; no `STAT`, `OPEN flags WRITE`, `WRITE off 5 len 8`; 25 bytes, bytes 5-12 replaced
  - `-C 5` against a missing remote: `[0]` / `curl: (78) Upload failed: No such file or directory (2/-31)` / 78
  - `-C 13` and `-C 20` against a 5-byte remote: `[0]` / empty / 0; `OPEN flags WRITE`, `CLOSE`; unchanged
  - `-a` (existing `prefix-`, and missing): `[13]` / empty / 0; `OPEN flags WRITE,APPEND,CREATE`, `WRITE off 0 len 13`; 20 bytes, and 13
  - `-a -C 5`, `-a -C -` (still sends `STAT`): `[13]` / empty / 0; `WRITE off 0 len 13`, the whole source appended
  - 3,000,000 random bytes: `[3000000]` / empty / 0; `WRITE`s of 30000, 30000, 5536 per 65536-byte block
  - `-T -` of the 13 bytes: `[13]` / empty / 0; of 3 MB: `[3000000]`, pipe-sized writes (4176, 5552, ...)
  - `-T -` with `-C 5`, 5-byte remote: `[13]` / empty / 0; `OPEN flags WRITE`, `WRITE off 5 len 13`: standard input is not skipped; 18 bytes
- **Measured against the scripted subsystem**: an empty source: `OPEN`, `CLOSE`, `[0]` / 0;
  `WRITE` answered 3: `[0]` / `curl: (79) Error in the SSH layer` / 79, `CLOSE` still sent;
  3 MB with the second `WRITE` answered 4: `[30000]` / same / 79, `CLOSE` after the block's
  three writes; connection killed at `WRITE`: `[0]` / same / 79; `WRITE` answered with
  `DATA`: curl hung until killed; `OPEN` answered 1 / 4 / 11: `Upload failed: Unknown error
  in libssh2 (1/-31)` 79 / `Operation failed (4/-31)` 79 / `File already exists (11/-31)` 73;
  with `--ftp-create-dirs`: first `OPEN` answered 2, 4 or 10 makes directories (`MKDIR`
  attributes `00000004 000041ED`), 3 does not; `MKDIR` answered 3, 4 or 11 goes on, then a
  failed reopen is `curl: (78) Creating the dir/file failed: No such file or directory`;
  `MKDIR` answered 2 / 1 / 5 stops at once: `curl: (78) Remote file not found` / `(79) Error in
  the SSH layer` / `(79) Error in the SSH layer`; `/x.txt` needs no `MKDIR`; `/~/` paths make
  directories from `/home` down the resolved path; `STAT` with no size or size 0 under `-C -`
  uploads as new; a URL ending in `/` had the file name appended by the curl tool.
- **Decisions (ADR-0244, decided under Stewart's delegation):** open flags, `MKDIR` 0755
  and the create-directories statuses as measured; 64 KiB reads split into `WRITE`s of
  30000; a block's statuses are read before the next block is sent (libssh2 pipelines
  further, unobservably); a `WRITE` answered with another type fails at once with exit 79
  rather than hanging; a source that fails to read ends the upload as its end does, as the
  FTP upload takes it; `%{size_upload}` is the acknowledged bytes, set as
  `TransferReport.UploadSize`. The ADR is numbered 0244: 0243 went to another lane's AWS SigV4 ADR, and other lanes take numbers in
  the 023x range and 0242 may be taken concurrently.
- **Append.** `-a`/`--append` already reaches `ITransferContext.Append`, so append is done
  here rather than left to row 24's task.
- **Scope.** The ADR and its index line (`Documentation/Planning/Decisions`) sit outside
  `touches`, as every SSH task's ADR does; no task in `Doing` names `Documentation`. The
  console reaches this code once BL-576 registers the SSH handler. `--ai-help` is unchanged:
  no option was added or changed. SCP uploads stay with BL-577.
- **Results.** `Curl.Protocol.Ssh.UnitTests` passes 874 tests (43 new: `SftpFileUploadTests`
  and two handler tests through `InMemorySshServer`, which now stores writes and creates a
  file on an open with `SSH_FXF_CREAT`); the fast run passes in all 33 test projects;
  `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary`: 100% line, 100% branch,
  490 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. sftp:// -T uploads as curl 8.21.0 does: create/truncate, -C and -C - resume, -a append, --create-file-mode, --ftp-create-dirs MKDIRs, 30000-byte WRITEs, measured failures; ADR-0244
