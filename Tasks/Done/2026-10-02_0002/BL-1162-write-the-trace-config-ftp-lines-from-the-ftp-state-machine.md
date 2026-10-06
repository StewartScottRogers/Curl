---
id: BL-1162
title: Write the --trace-config ftp lines from the FTP state machine
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1162 — Write the --trace-config ftp lines from the FTP state machine

## Goal

Under `-v --trace-config ftp` (and `protocol`, `all`) Curl writes curl 8.21.0's `* [FTP] ...` lines from its FTP state machine, at the same steps and with the same text.

## Context

- Split from BL-1104 (ADR-0318). Follow BL-1102's pattern: `Curl.Console` (`CurlComposition`) decides whether the component is on (`ftp`, `protocol` or `all` in `CommandLineOptions.TraceComponents`) and hands the FTP handler an `ITransferEvents` sink; the FTP library writes each line at its own step.
- Measured lines are in Notes. `-vv` puts `protocol` among the components too (ADR-0318), so it turns these lines on.

## Acceptance criteria

- [x] A plain `ftp://` download under `-v --trace-config ftp` writes the measured `[FTP]` lines in Notes, in that order, between the usual `-v` lines; a test pins them.
- [x] `protocol` and `all` write the same lines; `-v` alone, `--trace-config smtp` and `--trace-config ftp` without `-v` write none (tests).
- [x] Other FTP paths (upload, active mode, error replies) are measured with `Record-CurlExchange.ps1 -Ftp` and pinned, or filed as follow-up tasks.
- [x] `--ai-help` still describes `--trace-config` correctly.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

Measured 2026-10-02 (BL-1104), curl 8.21.0 Schannel,
`Record-CurlExchange.ps1 -Ftp -FtpData 'hello\n' -CurlArgs '-sS','--trace-config','ftp','-v','ftp://127.0.0.1:P/a.txt'`, the `[FTP]` lines in order:

```
* [FTP] [STOP] setup connection -> 0
* [FTP] [STOP] -> [WAIT220]
* [FTP] [WAIT220] -> [USER]
* [FTP] [USER] -> [PASS]
* [FTP] [PASS] -> [PWD]
* [FTP] [PWD] -> [STOP]
* [FTP] [STOP] protocol connect phase DONE
* [FTP] [STOP] DO phase starts
* [FTP] [STOP] -> [PASV]
* [FTP] [PASV] perform, awaiting DATA connect
* [FTP] [PASV] -> [STOP]
* [FTP] [STOP] DO phase is complete2
* [FTP] [STOP] ftp_domore_pollset()
* [FTP] [STOP] -> [RETR_TYPE]
* [FTP] [RETR_TYPE] ftp_domore_pollset()
* [FTP] [RETR_TYPE] -> [RETR_SIZE]
* [FTP] [RETR_SIZE] ftp_domore_pollset()
* [FTP] [RETR_SIZE] ftp_state_retr()
* [FTP] [RETR_SIZE] -> [RETR]
* [FTP] [RETR] ftp_domore_pollset()
* [FTP] ftp_initiate_transfer()
* [FTP] [RETR] -> [STOP]
* [FTP] [STOP] closing DATA connection
* [FTP] getftpresponse start
* [FTP] getftpresponse -> result=0, nread=23, ftpcode=226
* [FTP] [STOP] done, result=0
```

`--trace-config protocol -v` wrote the same 26 lines; `--trace-config ftp` without `-v` wrote nothing; `--trace-config smtp -v` wrote no `[FTP]` line. Re-record the full stderr to place them among the `-v` lines.

Delivered 2026-10-02 (lane 3):

- `FtpStateTrace` (new, `Curl.Protocol.Ftp.UnitLibrary`) writes the lines through the transfer's `ITransferEvents.ReportInfo`; `FtpProtocolHandler.TracesStateMachine` turns it on, set by `CurlComposition.CreateFtpProtocolHandler` from `CurlTransports.TracesFtp` = `CurlComposition.TracesFtp(options)` (`ftp`, `protocol` or `all`). Without `-v` or `--trace` no events exist, so nothing is written, as curl.
- Each state change is written after its command is sent, from the command's verb; the DO_MORE poll before `Established 2nd connection` is written by `FtpDataConnectEvents` as the data connection opens. `nread` is the reply's byte count, every line's CR LF included (`FtpReply.ByteCount`).
- Measured and pinned: passive download, passive upload (`STOR_TYPE`, `STOR`) and listing (`LIST_TYPE`, `LIST`). Running the built Curl against `Record-CurlExchange.ps1 -Curl` gave stderr identical to curl 8.21.0's for all three, port numbers aside.
- Measured too: `-vv` and `-vvv` write all 26 lines. Curl's `-vv` added only `setup`, so `Curl.Cli.UnitLibrary`'s `VerbosityTraceComponentsAt(2)` now adds `protocol` as well. `Curl.Cli.UnitLibrary` and `Curl.Cli.UnitTests` were added to `touches` for this: no task in Doing on `origin/work/dark-factory` names them (BL-1171 touches Tls/Networking, BL-1184 `.github/board`).
- Active mode and error replies were measured (the lines are in BL-1196 and BL-1197) and filed as follow-ups, with the other states (`CWD`, `MDTM`, `REST`, `AUTH`, ...), which write no state change yet.
- `--ai-help` describes `--trace-config` as "Details to log in trace/verbose output", which is still true; no option changed.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. -v --trace-config ftp/protocol/all and -vv write curl's [FTP] state machine lines for passive downloads, uploads and listings
