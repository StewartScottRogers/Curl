---
id: BL-974
title: Treat an sftp:// URL path of /~ as the home directory, as curl does
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-30
---
# BL-974 — Treat an sftp:// URL path of /~ as the home directory, as curl does

## Goal

`sftp://host/~` resolves to the home directory and a slash, as curl 8.21.0's `Curl_getworkingpath` resolves it, and a home directory that already ends in `/` gains no second slash before the rest of a `/~/` path.

## Context

- Measured 2026-09-29 in BL-572: `curl -D - -Q pwd sftp://localhost:2233/~` printed `257 "/home/stewart_rogers/" is current directory.` and listed the home directory (exit 0). `SftpRemotePath.Resolve` only handles `/~/`, so Curl would try to download a file named `/~`.
- curl's `Curl_getworkingpath` (`lib/vssh/vssh.c` at `curl-8_21_0`): for SFTP, `/~` or a path starting `/~/` becomes the home directory, then the rest from index 2 when the home directory does not end with `/`, or from index 3 when it does; `/~` alone becomes the home directory and `/`. Its `REJECT_ZERO` decoding also refuses a `%00` in the path.
- **BCL first.** Anything the BCL lacks is hand-built in its own library (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28).

## Acceptance criteria

- [x] Measured first with the reference curl against OpenSSH (BL-572's setup): `/~`, `/~/`, `/~/x`, and a `%00` in the path; stdout, stderr and exit code copied into Notes.
- [x] `SftpRemotePathTests` pin each resolution, and `SftpDirectoryListingTests` or `SshProtocolHandlerTests` pin that `/~` lists the home directory.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- **Setup.** BL-572's WSL OpenSSH 10.2 on port 2233 (`~/bl572/sshd_config`), started with
  `LD_LIBRARY_PATH=~/bl569/root/usr/lib/x86_64-linux-gnu` (without it every connection
  failed `Failed getting banner`, exit 2, because the re-exec'd `sshd` could not load
  `libwrap`). Reference curl 8.21.0 (Git for Windows' mingw64, libssh2 1.11.1) through
  `Record-CurlExchange.ps1 -NoServer -CurlArgs -sS,-k,--key,<clientkey>,--pubkey,<pub>,-u,stewart_rogers:,<url>`.
  Home directory `/home/stewart_rogers`. `curl-8_21_0`'s `lib/vssh/vssh.c` was read for
  `Curl_getworkingpath`.
- **Measured 2026-09-30** (stdout / stderr / exit; `sftp-server` requests after `INIT`):
  - `/~`: the home directory's listing / empty / 0; `REALPATH .`, `OPENDIR "/home/stewart_rogers/"`
  - `/~` with `-D - -Q pwd`: `257 "/home/stewart_rogers/" is current directory.\n` then the listing / empty / 0
  - `/~/`: the same listing / empty / 0
  - `/~/bl572/files/a.txt`: `hello\n` / empty / 0; `OPEN "/home/stewart_rogers/bl572/files/a.txt"`
  - `/~/bl572/files/a%00.txt`: empty / `curl: (3) URL using bad/illegal format or missing URL` / 3; only `REALPATH .`, then channel `EOF`, `CLOSE`, `DISCONNECT 11 Shutdown` in the `sshd` log
  - `/~bl572`: empty / `curl: (78) Could not open remote file for reading: No such file or directory` / 78; `OPEN "/~bl572"`
- **Decisions (ADR-0277, decided under Stewart's delegation):** port `Curl_getworkingpath`'s
  SFTP branch into `SftpRemotePath.Resolve` (`/~` is home + `/`; `/~/rest` takes the rest
  from index 3 when the home directory is empty or ends with `/`, else from index 2);
  `NamesDirectory` is true for `/~`; `ResolveUrlPath` decodes, refuses a zero byte with
  `SshTransferException.UrlPathHoldsZeroByte` (exit 3, the exit code's text, no `-v` line,
  as curl has no `failf` there), and resolves, inside `CloseChannelOnFailureAsync`, so the
  channel closes before `DISCONNECT` for downloads, listings and uploads. A home of `/` was
  not measured and follows the source (`/~` becomes `//`). ADR-0220 points at ADR-0277.
- **Scope.** The ADR, its index line and the one-line pointer in ADR-0220 sit in
  `Documentation`, outside `touches`, as every SSH task's ADR does; BL-615, the only other
  task in `Doing`, does not name `Documentation`. `--ai-help` is unchanged: no option was
  added or changed. SCP's `%00` handling was not measured; it is left as it is.
- **Aside.** A full (not fast) run of `Curl.Protocol.Ssh.UnitTests` hung in the
  `Integration` test `SystemSshAgentConnectorTests.ConnectAsync_WindowsPipeServed_ConnectsToIt`
  (BL-902); the fast run excludes it and it is unrelated to this change.
- **Results.** `Curl.Protocol.Ssh.UnitTests` passes 1404 fast tests, among them the new
  `SftpRemotePathTests` rows, `ExecuteAsync_SftpHomeDirectoryPath_ListsTheHomeDirectoryAsMeasured`
  and `ExecuteAsync_SftpPathHoldsZeroByte_IsExit3AfterRealPathAndClosesTheChannel`; all 33
  fast test projects pass. `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary`:
  100% line, 100% branch, 795 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. sftp://host/~ lists the home directory, /~/ joins a home ending in / without a second slash, and a %00 in the path is exit 3 after REALPATH with the channel closed, as curl 8.21.0 does; ADR-0277
