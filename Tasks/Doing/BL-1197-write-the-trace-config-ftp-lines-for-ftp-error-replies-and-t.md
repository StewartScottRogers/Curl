---
id: BL-1197
title: Write the --trace-config ftp lines for FTP error replies and the remaining states
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1197 — Write the --trace-config ftp lines for FTP error replies and the remaining states

## Goal

Under `-v --trace-config ftp` every FTP path BL-1162 left unmeasured writes curl 8.21.0's `[FTP]` lines: error replies, `CWD`, `MDTM`, `REST`, `SIZE` on an upload, `PASV` after a refused `EPSV`, `AUTH`/`PBSZ`/`PROT`/`CCC`, `SYST`, quotes and `ACCT`.

## Context

- Follow-up of BL-1162. `FtpStateTrace` (`Curl.Protocol.Ftp.UnitLibrary`) maps only `USER`, `PASS`, `PWD`, `EPSV`/`PASV`, `TYPE`, `SIZE` and the transfer verbs to states; any other command writes no state change, and a failing transfer writes no end lines.
- Measured 2026-10-02 (BL-1162): `RETR` answered `550` writes, after `[RETR] ftp_domore_pollset()`, `[RETR] closing DATA connection` and `[RETR] done, result=0` (exit 78). Measure the rest with `Record-CurlExchange.ps1 -Ftp -FtpReply` before pinning.

## Acceptance criteria

- [ ] Tests in `Curl.Protocol.Ftp.UnitTests` pin the measured `[FTP]` lines for a refused `RETR`, a `CWD` path, `-R` (`MDTM`), `-C` (`REST`), `--disable-epsv` and `--ssl`.
- [ ] The remaining verbs are measured and pinned, or filed as further tasks.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
