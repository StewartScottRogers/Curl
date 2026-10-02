---
id: BL-1196
title: Write the --trace-config ftp lines for active mode (-P)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1196 — Write the --trace-config ftp lines for active mode (-P)

## Goal

Under `-v --trace-config ftp` an active-mode (`-P`) FTP download writes curl 8.21.0's `[FTP]` lines, in curl's order among the `-v` lines.

## Context

- Follow-up of BL-1162, which wrote the passive download, upload and listing lines through `FtpStateTrace` (`Curl.Protocol.Ftp.UnitLibrary`). Active mode writes nothing for `EPRT`/`PORT` yet.
- Measured 2026-10-02 (BL-1162), `-sS --trace-config ftp -v -P 127.0.0.1 ftp://127.0.0.1:P/a.txt`, the `[FTP]` lines after `DO phase starts`:
  `[STOP] ftp_state_use_port(), opened socket`, `ftp_port_bind_socket(), socket bound to port 0`, `ftp_port_listen(), listening on port`, then after `> EPRT`: `[STOP] -> [PORT]`, `[PORT] perform, awaiting DATA connect`, `[PORT] -> [STOP]`, `[STOP] DO phase is complete2`; then TYPE/SIZE/RETR as passive, but after `[RETR] ftp_domore_pollset()` come `[RETR] -> [STOP]`, `[STOP] ftp_domore_pollset()`, `ftp_initiate_transfer()`. Re-record full stderr to place them, and measure `PORT` (`--disable-eprt`).

## Acceptance criteria

- [x] A test in `Curl.Protocol.Ftp.UnitTests` pins the measured active-mode `[FTP]` lines in order among the `-v` lines, `EPRT` and `PORT` both.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

Measured 2026-10-02, curl 8.21.0 Schannel, `Record-CurlExchange.ps1 -Ftp -Port 47196 -FtpData 'hello\n' -CurlArgs '-sS','--trace-config','ftp','-v','-P','127.0.0.1','ftp://127.0.0.1:47196/a.txt'`, with and without `--disable-eprt`, and an active upload (`-T`). EPRT and PORT write the same lines. The DO phase, after `DO phase starts`:

```
* [FTP] [STOP] ftp_state_use_port(), opened socket
* [FTP] ftp_port_bind_socket(), socket bound to port 0
* [FTP] ftp_port_listen(), listening on port
> EPRT |1|127.0.0.1|60355|
* [FTP] [STOP] -> [PORT]
* [FTP] [PORT] perform, awaiting DATA connect
< 200 EPRT command successful
* Connect data stream actively
* [FTP] [PORT] -> [STOP]
* [FTP] [STOP] DO phase is complete2
```

The download, after `Getting file with size: 6`: `Data conn was not available immediately`, `[RETR] -> [STOP]`, `[STOP] ftp_domore_pollset()`, `Ready to accept ...`, `Connection accepted from server`, `Established 2nd connection ...`, `ftp_initiate_transfer()` - no state change after it. The upload writes `[STOR] -> [STOP]` *before* `Data conn was not available immediately`, the rest as the download.

Delivered: `FtpStateTrace` maps `EPRT`/`PORT` to state `PORT`, writes the listen lines (`ActivePortListening`, after a successful bind in `FtpSession.ListenAsync`), the DO phase end after `Connect data stream actively`, and the accept poll (`AcceptPending`). `TransferInitiated` now runs after the data connection is ready and only changes state when not already `STOP`; passive output is unchanged because passive readiness writes nothing. Pinned by three tests in `FtpProtocolHandlerStateTraceTests` (EPRT download, PORT download, EPRT upload). Not measured, so left as is: a refused EPRT followed by PORT writes the listen lines again on the second bind.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. -v --trace-config ftp writes curl's [FTP] lines for active-mode (-P) downloads and uploads, EPRT and PORT
