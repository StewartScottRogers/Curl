---
id: BL-572
title: Run SFTP -Q quote commands before and after the transfer
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-569]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Curl.Protocol.Abstractions.UnitLibrary]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-572 — Run SFTP -Q quote commands before and after the transfer

## Goal

`-Q`/`--quote` commands on an SFTP URL (`chgrp`, `chmod`, `chown`, `ln`, `mkdir`, `pwd`, `rename`, `rm`, `rmdir`, `symlink`, `atime`, `mtime`, with `-` for after the transfer and `*` to ignore failure) run as curl 8.21.0 runs them, with an unknown or failing command mapped to exit 21 and curl's message.

## Context

- Conformance audit 2026-09-28, row 35. `-Q` is already parsed and carried as `ITransferContext.QuoteCommands` (the FTP handler uses it). The SFTP command list is in `CurlManual.txt` (`--quote`).
- **BCL first.** Anything the BCL lacks is hand-built in its own library (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28): never a package, never a task blocked for a missing primitive.
- Measure with the reference curl against a local OpenSSH server through `Record-CurlExchange.ps1 -NoServer`: each command once, a quoted argument with a space, `pwd` (what it prints), an unknown command, a failing `rm` with and without `*`, and a `-` post-transfer command.

## Acceptance criteria

- [x] Measured first as above; stdout, stderr and exit code copied into Notes.
- [x] `Curl.Protocol.Ssh.UnitTests` pin the SFTP request each command sends and the outcome for each case against the in-memory peer.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- **Measurement setup.** BL-571's WSL OpenSSH 10.2 (`~/bl572`: `sshd -D` on port 2233,
  public-key auth, `LogLevel DEBUG3`, `sftp-server -l DEBUG3` behind a wrapper logging every
  request). The reference curl 8.21.0 (libssh2 1.11.1, Schannel) ran through
  `Record-CurlExchange.ps1 -NoServer` as `curl -sS -k --key <rsa> --pubkey <pub> -u <user>:
  -Q <command> sftp://localhost:2233/~/bl572/files/a.txt`; `a.txt` holds `hello\n`. Ubuntu's
  curl 8.18.0 (libssh2 1.11.1, OpenSSL) ran against the same server for the 64-bit `long`
  cases. curl 8.21.0's `lib/vssh/libssh2.c`, `vssh.c` and `curlx/strparse.c` were read at
  the `curl-8_21_0` tag to explain every result. `/f` below is `/home/stewart_rogers/bl572/files`.
- **Measured** (stdout / stderr / exit; requests after `INIT` and `REALPATH .`):
  - `mkdir /f/nd`: `hello\n` / empty / 0; `MKDIR mode 0755`, then `OPEN`, `STAT`, `READ`, `CLOSE`
  - `rmdir /f/dd`, `rm /f/b.txt`, `rename /f/b.txt /f/c.txt`: `hello\n` / empty / 0; `RMDIR`, `REMOVE`, `RENAME old new`
  - `ln /f/b.txt /f/l1` and `symlink /f/b.txt /f/l2`: `hello\n` / empty / 0; `SYMLINK old /f/b.txt new /f/l1` (and `l2`)
  - `chmod 640 /f/b.txt`: `hello\n` / empty / 0; `SETSTAT mode 0640` only, no `STAT`
  - `chown 1000 /f/b.txt`, `chgrp 1000 /f/b.txt`: `hello\n` / empty / 0; `STAT`, then `SETSTAT owner 1000 group 1000`
  - `atime "Thu, 02 Jan 2020 03:04:05 GMT" /f/b.txt`, `mtime "Tue, 1 Jan 2019 10:00:00 GMT" /f/b.txt`: 0; `STAT`, `SETSTAT` of both times (the other kept)
  - `atime 2020-01-02T03:04:05Z /f/b.txt`: empty / `curl: (21) incorrect date format for atime` / 21; `STAT` sent first
  - `mtime "1 Jan 2200" /f/b.txt`: empty / `curl: (21) date overflow` / 21; on Linux 8.18: 0, `SETSTAT modtime` 2063-11-24 (low 32 bits)
  - `chmod 999`, `chmod 17777`: `curl: (21) Syntax error: chmod permissions not a number` / 21; no request
  - `chgrp abc /f/b.txt`: `curl: (21) Syntax error: chgrp gid not a number` / 21; `STAT` sent
  - `*chgrp abc /f/b.txt`: `hello\n` / empty / 0; `STAT`, `SETSTAT` of size, mode, times, owner and group as `STAT` gave them
  - `chown 1000 /f/zz`: `curl: (21) Attempt to get SFTP stats failed: No such file or directory` / 21
  - `*chown 1000 /f/zz`: `hello\n` / empty / 0; `STAT` answered 2, `SETSTAT owner 1000 group 0` answered 2
  - `chmod 644 /f/zz`: `curl: (21) Attempt to set SFTP stats for "/f/zz" failed: No such file or directory` / 21; `*chmod 644 /f/zz`: 0
  - Linux 8.18: `chown 1000 ...`, `chgrp 1000 ...`, `chgrp 0 ...`: `Syntax error: chown uid not a number` / `chgrp gid not a number` / 21 (curl's `ULONG_MAX` is -1 to its own parser)
  - `mkdir /f/dd` (exists): `curl: (21) mkdir "/f/dd" failed: Operation failed` / 21
  - `rmdir /f/zz`: `curl: (21) rmdir "/f/zz" failed: No such file or directory`; `rename /f/zz /f/yy`: `curl: (21) rename "/f/zz" to "/f/yy" failed: No such file or directory`; `ln /f/b.txt /f/dd`: `curl: (21) symlink "/f/b.txt" to "/f/dd" failed: Operation failed`; all 21
  - `rm /f/zz`: empty / `curl: (21) rm "/f/zz" failed: No such file or directory` / 21; `*rm /f/zz`: `hello\n` / empty / 0
  - `-D - -Q "statvfs /f"`: `statvfs:\nf_bsize: 4096\nf_frsize: 4096\nf_blocks: 263940717\nf_bfree: 233486291\nf_bavail: 220060423\nf_files: 67108864\nf_ffree: 66840330\nf_favail: 66840330\nf_fsid: 15601863864774658782\nf_flag: 0\nf_namemax: 255\nhello\n` / empty / 0
  - `statvfs /f/zz`: `curl: (21) statvfs "/f/zz" failed: No such file or directory` / 21
  - `pwd`: `hello\n` / empty / 0 (with `-i` too); with `-D -`: `257 "/home/stewart_rogers/bl572/files/a.txt" is current directory.\nhello\n`; `PWD` the same
  - `-D - -Q -pwd`: `hello\n257 "(nil)" is current directory.\n` / empty / 0
  - `-Q "-rm /f/zz"`: `hello\n` / `curl: (21) rm "/f/zz" failed: No such file or directory` / 21; `REMOVE` after the download's `CLOSE`
  - `-Q "-mkdir /f/nd"` on a missing file: `curl: (78) Could not open remote file for reading: No such file or directory` / 78; no `MKDIR`
  - `-Q "+rm /f/zz"`: `hello\n` / empty / 0; no `REMOVE`
  - `foo bar`, `*foo bar`: `curl: (21) Unknown SFTP command`; `foo`: `curl: (21) Syntax error command 'foo', missing parameter`; `rm`: `... 'rm', missing parameter`; `rm<TAB>/f/b.txt`: `... 'rm	/f/b.txt', missing parameter`; all 21
  - `rm `: `curl: (21) Syntax error: Bad first parameter to 'rm '`; `rm ""` and `rm "a\b"` the same with their text; 21
  - `chmod 644`: `Syntax error in chmod 644: Bad second parameter`; `ln /f/b.txt`: `Syntax error in ln/symlink: Bad second parameter`; `rename /f/b.txt`: `Syntax error in rename: Bad second parameter`; 21
  - `rm /f/b.txt more`, `rm "/f/b.txt"x`, `rm /~/bl572/files/b c.txt`: `curl: (21) Suspicious data after the command line` / 21
  - `rm "/f/b c.txt"` and `rm '/f/b c.txt'`: 0; `REMOVE "/f/b c.txt"`
  - `rmdir /~/`: `curl: (21) rmdir "/home/stewart_rogers/" failed: Permission denied` / 21
  - Upload `-T up.txt -C - -Q "rm /f/b.txt" -Q "-mkdir /f/nd"`: `REMOVE`, `STAT`, `OPEN`, `WRITE`, `CLOSE`, `MKDIR`, exit 0
  - Listing `/~/bl572/files/` with `-Q "rm ..." -Q "-mkdir ..." -Q pwd -D -`: `REMOVE`, `OPENDIR`, `READDIR`, `CLOSE`, `MKDIR`; `257 "/home/stewart_rogers/bl572/files/" is current directory.` first
  - Teardown (`sshd` log): success, a failed plain command, a failed `-` command and a failed `OPEN` all send `EOF` and `CLOSE` on the channel before `DISCONNECT 11 Shutdown`
- **Decisions (ADR-0247, decided under Stewart's delegation):** port `sftp_quote`,
  `sftp_quote_stat` and `Curl_get_pathname` as measured; `+` values are dropped; `pwd` and
  `statvfs` write to `HeaderOutput` (`-D`); the handler passes `OperatingSystem.IsWindows()`
  as "C `long` is 32 bits", so `chown`/`chgrp` read numbers and dates overflow or truncate as
  each platform's curl does; `STAT` answered OK is empty attributes and `SETSTAT` answered
  with attributes is success, as libssh2 takes them; an unexpected answer type, a cut-short
  `statvfs` answer or a broken connection is exit 79 rather than curl's hang; a failure before
  the transfer throws as a failed open does (channel left for `DISCONNECT`), a failure after
  it keeps the transfer's bytes and `%{size_upload}` and closes the channel. The ADR is
  numbered 0247 to stay clear of the numbers other lanes may take concurrently.
- **Scope.** `Curl.Protocol.Abstractions.UnitLibrary` was added to `touches` for a one-line
  doc fix: `ITransferContext.QuoteCommands` said only `ftp://` interprets the values. No task
  in `Doing` names it. The ADR and its index line sit outside `touches`, as every SSH task's
  ADR does; no task in `Doing` names `Documentation`. `--ai-help` is unchanged: no option was
  added or changed. SCP's `-Q` is not in this task.
- **Follow-ups filed.** BL-973: close the channel after a failed open or quote command, as
  measured (the existing failures leave it open, per ADR-0220). BL-974: `sftp://host/~`
  resolves to the home directory, as curl lists it.
- **Results.** `Curl.Protocol.Ssh.UnitTests` passes 979 tests, the new
  `SftpQuoteCommandTests`, `SftpQuoteCommandsTests`, `SftpTransferQuoteTests` and two handler
  tests among them. The fast run
  passes in all 33 test projects. `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary`:
  100% line, 100% branch, 548 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. sftp:// -Q runs chgrp, chmod, chown, atime, mtime, ln, symlink, mkdir, rename, rmdir, rm, statvfs and pwd before and (with -) after the transfer as curl 8.21.0 does, * passing server failures, exit 21 with curl's messages; ADR-0247
