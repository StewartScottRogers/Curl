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
completed: 2026-10-02
---
# BL-1197 — Write the --trace-config ftp lines for FTP error replies and the remaining states

## Goal

Under `-v --trace-config ftp` every FTP path BL-1162 left unmeasured writes curl 8.21.0's `[FTP]` lines: error replies, `CWD`, `MDTM`, `REST`, `SIZE` on an upload, `PASV` after a refused `EPSV`, `AUTH`/`PBSZ`/`PROT`/`CCC`, `SYST`, quotes and `ACCT`.

## Context

- Follow-up of BL-1162. `FtpStateTrace` (`Curl.Protocol.Ftp.UnitLibrary`) maps only `USER`, `PASS`, `PWD`, `EPSV`/`PASV`, `TYPE`, `SIZE` and the transfer verbs to states; any other command writes no state change, and a failing transfer writes no end lines.
- Measured 2026-10-02 (BL-1162): `RETR` answered `550` writes, after `[RETR] ftp_domore_pollset()`, `[RETR] closing DATA connection` and `[RETR] done, result=0` (exit 78). Measure the rest with `Record-CurlExchange.ps1 -Ftp -FtpReply` before pinning.

## Acceptance criteria

- [x] Tests in `Curl.Protocol.Ftp.UnitTests` pin the measured `[FTP]` lines for a refused `RETR`, a `CWD` path, `-R` (`MDTM`), `-C` (`REST`), `--disable-epsv` and `--ssl`.
- [x] The remaining verbs are measured and pinned, or filed as further tasks.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

Measured 2026-10-02, curl 8.21.0 Schannel, `Record-CurlExchange.ps1 -Ftp -FtpData 'hello\n' -CurlArgs '-sS','--trace-config','ftp','-v',...` with `-FtpReply` overrides. What curl does:

- **State per command**: `CWD`, `MDTM`, `ACCT`, `SYST`, `AUTH`, `PBSZ`, `PROT`, `CCC` each enter a state of their own name; `REST` enters `RETR_REST`; a quote after login enters `QUOTE`, a `+` quote `<transfer>_PREQUOTE`; a `-` quote after the transfer changes no state and its reply is read as `getftpresponse start` / `getftpresponse -> result=0, nread=N, ftpcode=C` before `done`. A command that stays in the same state (a second `CWD`, `PASV` after a refused `EPSV`) writes no change line.
- **`perform, awaiting DATA connect`** is written once per DO phase, after its first command, whatever state that is (`[CWD]`, `[MDTM]`, `[QUOTE]`, `[PASV]`, `[PORT]`), not after `EPSV`/`PASV` as such.
- **`ftp_state_retr()`** comes right after the `SIZE` reply, before `Instructs server to resume from offset 2` and `REST`.
- **Refused `EPSV`**: `Failed EPSV attempt. Disabling EPSV`, then `[PASV] closing DATA connection`, then `PASV`.
- **End of a failed transfer**: `[STATE] DO phase failed` when it failed inside the DO phase (refused `CWD`, refused quote), `[STATE] closing DATA connection` when the DO phase had completed (refused `RETR`, `SIZE` 550), then `[STATE] done, result=N`. N is 0 for the failures curl's `ftp_done` treats as leaving the connection usable (measured: 9 and 78) and the exit code otherwise (measured: 8 refused greeting, 21 refused quote, 67 refused `PASS`, 81 `CCC` on Schannel). The full keep-list (36, 13, 30, 10, 12, 17, 19, 18, 25, 9, 63, 78, 23) is taken from curl's `ftp_done`; only 9 and 78 were measured.
- **`-r 0-1`**: `Remembering...`, `> ABOR`, `[STOP] closing DATA connection`, `getftpresponse start`, ABOR's reply, `getftpresponse -> ...`, `partial download completed, closing connection`, `[STOP] done, result=0`, `shutting down connection #0`.
- **Upload `-C -`**: `SIZE` enters `STOR_SIZE`, `APPE` then `STOR` — it already worked; now pinned.

Delivered: `FtpStateTrace` maps the verbs above, writes the perform note once per DO phase, skips same-state changes, writes `EpsvRefused`, the quote states (`FtpQuoteStage`, new) and `Ended(CurlExitCode)` once per session (from `QuitAndSucceedAsync` after the post-quotes, from `EndRangeAsync`, and from `RunAsync` for any failure). `FtpSession` moved `ftp_state_retr()` to after `SIZE`, traces `ABOR`'s reply for a range, and sends quotes through `SendAsync` so their states are traced. 14 new tests in `FtpProtocolHandlerStateTraceTests`, two updated (the `CWD` state and the refused greeting's `done, result=8`); the test `MutableContext` gained `RemoteTime` and `FtpAccount`.

Left as is, not measured: `PRET`, `SITE NAMEFMT 1` and `MKD` write no state change; a range whose post-quote is refused writes `done, result=0` (the end is written before the quotes there). Filed as BL-1199.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. -v --trace-config ftp writes curl's [FTP] lines for CWD, MDTM, REST, AUTH/PBSZ/PROT, SYST, ACCT, quotes, refused EPSV, ranges and failed transfers
