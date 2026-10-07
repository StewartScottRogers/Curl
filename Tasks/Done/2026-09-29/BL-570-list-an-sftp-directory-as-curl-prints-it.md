---
id: BL-570
title: List an SFTP directory as curl prints it
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-569]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-570 — List an SFTP directory as curl prints it

## Goal

`sftp://host/dir/` (a path ending in `/`) lists the directory with `OPENDIR`/`READDIR` and writes each entry exactly as curl 8.21.0 does (the long `ls -l`-style line the server's `longname` gives, or names only with `-l`/`--list-only`).

## Context

- Conformance audit 2026-09-28, row 35. Builds on BL-569.
- **BCL first.** Anything the BCL lacks is hand-built in its own library (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28): never a package, never a task blocked for a missing primitive.
- Measure with the reference curl against a local OpenSSH server through `Record-CurlExchange.ps1 -NoServer`: a directory with files, a subdirectory and a symbolic link, with and without `-l`, an empty directory, and a missing directory.

## Acceptance criteria

- [x] Measured first as above; stdout bytes, stderr and exit code copied into Notes.
- [x] `Curl.Protocol.Ssh.UnitTests` pin the output bytes for each case from canned `READDIR` replies.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- **Measurement setup.** BL-569's WSL OpenSSH 10.2 (`sshd -D`, public-key auth) with the
  reference curl 8.21.0 (libssh2 1.11.1, Schannel), run through
  `Record-CurlExchange.ps1 -NoServer` as `curl -sS -k --key <rsa> --pubkey <pub> -u <user>:`;
  once against OpenSSH's `sftp-server`, once against BL-569's scripted SFTP subsystem
  (throwaway C# app in `%TEMP%`, not committed) extended to answer `OPENDIR`, `READDIR`
  and `READLINK` by path and log each request.
- **Measured against `sftp-server`** (a directory with `a.txt`, `b.bin`, subdirectory `sub`,
  symbolic link `link -> a.txt` and broken link `broken -> /nonexistent/target`), stdout / stderr / exit:
  - `sftp://h/.../list/`: the seven lines below / empty / 0; with `-w '[%{size_download}]'` the same then `[541]`:
    ```
    drwxr-xr-x    2 stewart_rogers stewart_rogers     4096 Jan  1  2026 sub
    -rw-r--r--    1 stewart_rogers stewart_rogers        6 Jan  1  2026 a.txt
    drwxr-xr-x    4 stewart_rogers stewart_rogers     4096 Sep 29 12:51 ..
    lrwxrwxrwx    1 stewart_rogers stewart_rogers       19 Sep 29 12:50 broken -> /nonexistent/target
    drwxr-xr-x    3 stewart_rogers stewart_rogers     4096 Sep 29 12:50 .
    lrwxrwxrwx    1 stewart_rogers stewart_rogers        5 Sep 29 12:50 link -> a.txt
    -rw-r--r--    1 stewart_rogers stewart_rogers        3 Jan  1  2026 b.bin
    ```
  - same with `-l`, and `sftp://h/~/bl570/list/` with `-l`: `sub\na.txt\n..\nbroken\n.\nlink\nb.bin\n` / empty / 0
  - an empty directory: `drwxr-xr-x    4 ... ..\ndrwxr-xr-x    2 ... .\n` / empty / 0; with `--list-only`: `..\n.\n` / empty / 0
  - a missing directory, and a file named with a trailing slash: empty / `curl: (78) Could not open directory for reading: No such file or directory` / 78
  - `-I`: empty / empty / 0
- **Measured against the scripted subsystem** (requests after `INIT` and `REALPATH .`):
  - listing: `OPENDIR` path (no attributes), `READDIR`, one `READLINK <dir><name>` per link entry, `READDIR` again per `NAME` answer, `CLOSE`: `LONG-f\nLONG-l -> tgt\n` / empty / 0; `-l`: `f\nl\n`, no `READLINK`
  - `/~/sub/` opens `/home/fake/sub/` and reads `/home/fake/sub/l`; `/d/x%2F` lists `/d/x/`
  - `READLINK` answered status 2, or `NAME` with no names: `LONG-f\n` / `curl: (27) Out of memory` / 27, `CLOSE` sent; answered `OK`: `LONG-l -> l` / 0 (`[19]` bytes)
  - second `READDIR` answered 3: `LONG-f\n` / `curl: (9) Could not open remote file for reading: Permission denied :: -31` / 9, `CLOSE` sent
  - `READDIR` answered `NAME` with no names: empty / empty / 0
  - entry without permissions, and one with an empty long name: `LONG-x\n\n` / 0; entry with size, uid/gid, `S_IFLNK` permissions and times: followed with `READLINK`
  - `OPENDIR` answered 3 / 4 / 1: `curl: (9) Could not open directory for reading: Permission denied` / `(79) ... Operation failed` / `(79) ... Unknown error in libssh2`, no `CLOSE`; answered `OK` then a handle: listed normally
- **Decisions (ADR-0241, decided under Stewart's delegation):** list when the decoded path
  ends with `/`; long name, stopped at a NUL as curl's C strings stop, plus ` -> target`
  for `S_IFLNK` entries; `-l` writes the raw file name; `-I` stops before `OPENDIR`; a
  connection that breaks mid-listing is exit 79 with the bytes so far (the scripted kill
  did not break the connection, so this follows ADR-0220 rather than a measurement);
  attributes are read to their end, extended pairs included. The ADR is numbered 0241
  rather than the next free 0233 because other lanes are taking numbers in the 023x range
  at the same time.
- **Shared code.** `SftpSession.FinishIgnoringFailureAsync` and
  `SshConnectionFailure.ReportAsSshLayerErrorAsync` replace `SftpFileDownload`'s private
  copies, so the download and the listing close and report failures one way.
- **Scope.** The ADR and its index line (`Documentation/Planning/Decisions`) sit outside
  `touches`, as every SSH task's ADR does; no task in `Doing` names `Documentation`.
  `--ai-help` is unchanged: no option was added or changed (`-l` and `-I` already exist).
- **Results.** `Curl.Protocol.Ssh.UnitTests` passes 831 tests, the new listing, path and handler cases included; the fast run passes 19683 tests
  in 33 projects; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary`: 100%
  line, 100% branch, 465 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. sftp:// paths ending in a slash list the directory as curl 8.21.0 prints it: long names with symbolic links followed, names only with -l, measured failures; ADR-0241
