---
id: BL-636
title: Clear the FTPS command channel with CCC after login
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-634]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions, Record-CurlExchange.ps1]
requirement: none
created: 2026-09-28
completed: 2026-09-30
---
# BL-636 — Clear the FTPS command channel with CCC after login

## Goal

With `--ftp-ssl-ccc` on an FTPS session, the handler sends `CCC` after authenticating, shuts TLS down on the control connection (actively or passively per `--ftp-ssl-ccc-mode`) and continues in plain text, as curl 8.21.0 does, with a refused `CCC` handled as curl handles it.

## Context

- Conformance audit 2026-09-28, row 25 (Major). Options: BL-634.
- The control connection is upgraded through `ITlsProvider` (ADR-0102); going back to plain text needs the TLS connection to shut down (`SslStream.ShutdownAsync`) and hand back the inner stream, which the `IConnection` contract may not offer. If a contract change is needed, record it as an ADR marked "Decided by Claude under Stewart's delegation"; the Abstractions, Networking and Decisions paths are in `touches` for that reason.
- `Record-CurlExchange.ps1 -Ftp` with `-Tls` serves AUTH TLS; it may need a `CCC` reply and a TLS shutdown to measure: extend it.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1 -Ftp` (extended if needed): `--ftp-ssl-reqd --ftp-ssl-ccc -k` with `CCC` answered `200` and `500`, and `--ftp-ssl-ccc-mode active`; commands, stderr and exit code copied into Notes.
- [x] `Curl.Protocol.Ftp.UnitTests` pin the command sequence and that commands after `CCC` go out in plain text, through fake connections.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

### Measurements (2026-09-30)

`Record-CurlExchange.ps1 -Ftp` was extended: `CCC` is answered `200 CCC command successful`
(or an override), and after any reply below 500 the server sends `close_notify`, reads curl's
`close_notify` if that is what comes next (otherwise keeps the first plain byte), and serves
the rest in plain text. Added to `touches` because the task asks for the extension and no task
in Doing names the script (BL-885: Http; BL-990: Ssh).

Command: `-sS -v -k --ftp-ssl-reqd --ftp-ssl-ccc [--ftp-ssl-ccc-mode active] ftp://u:p@<host>:18121/f.txt`.
Every run went `AUTH SSL` (234, TLS), `USER u`, `PASS p`, `PBSZ 0`, `PROT P`, `CCC`.

curl 8.21.0 Schannel (Windows):

- `CCC` 200, passive (default) and active alike, and 450 too:
  ```
  > CCC
  < 200 CCC command successful
  * schannel: shutting down SSL/TLS connection with 127.0.0.1 port 18121
  * Failed to clear the command channel (CCC)
  * closing connection #0
  curl: (81) Failed to clear the command channel (CCC)
  ```
  exit 81, no `QUIT`. The same whether the server sends `close_notify` before or after reading
  curl's (tried both); curl does send its own `close_notify`.
- `CCC` 500 or 533: `PWD`, `EPSV`, `TYPE I`, `SIZE f.txt`, `RETR f.txt`, `QUIT` over TLS, exit 0.
- `CCC` 421: `* We got a 421 - timeout`, exit 28 (curl's usual 421; the handler already does this).

curl 8.18.0 OpenSSL (Linux, through WSL with `-ListenAddress 172.26.96.1`):

- Active, `CCC` 200 or 450: `* TLSv1.2 (OUT), TLS alert, close notify (256):`,
  `* TLSv1.2 (IN), TLS alert, close notify (256):`, then `PWD` and the rest in plain text, exit 0.
- Passive, `CCC` 200: `* TLSv1.2 (IN), TLS alert, close notify (256):` only (curl sends no
  `close_notify`), then `PWD` and the rest in plain text, exit 0.
- `CCC` 500: the rest over TLS, exit 0.

### Decisions (ADR-0279, decided by Claude under Stewart's delegation)

- `IConnection.ClearTlsAsync(sendCloseNotifyFirst, token)`, a default member returning `null`;
  `SslStreamConnection` implements it per matched build (OpenSSL clears, Schannel sends
  `close_notify` and fails); `PooledConnection` forwards it (implicit `ftps://`) and never pools
  a connection it was asked to clear.
- `ITransferContext.FtpCommandChannelClearing` (`Off`/`Passive`/`Active`), mapped by
  `TransferContextFactory` from `CommandLineOptions.FtpClearCommandChannel`.
- `FtpSession.ClearControlTlsAsync` sends `CCC` after `PROT` on a TLS control connection
  (explicit or implicit); 5xx keeps TLS (diagnostic-log warning); otherwise clears, or reports
  `-v` `Failed to clear the command channel (CCC)` and ends with exit 81, no `QUIT`.
- Not written: the Schannel `* schannel: shutting down SSL/TLS connection ...` and OpenSSL
  `TLS alert, close notify` `-v` lines; Curl writes neither build's TLS-layer chatter yet
  (compare BL-806).
- The built `curl.exe` against the recorder: `CCC` 200 gives exit 81 with the same stderr tail
  and no `QUIT`; `CCC` 500 goes on over TLS to exit 0.

### Follow-up

- BL-1041: `HandBuiltTlsConnection.ClearTlsAsync` (the `--tls-max 1.0`/`1.1` and `--cert-status`
  route still ends `CCC` with exit 81).

### Gates

`dotnet build Curl.slnx -warnaserror` clean; fast tests all green (Ftp 510, Networking 1786,
Abstractions 636, Console 1915 passed); `Measure-CodeQuality.ps1`: Curl.Protocol.Ftp,
Curl.Protocol.Abstractions, Curl.Networking and Curl.Console at 100% line, 100% branch, 0 failing.

## Log

- 2026-09-28: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. --ftp-ssl-ccc sends CCC after PROT: plain text after it matching OpenSSL, exit 81 matching Schannel, TLS kept after a 5xx
