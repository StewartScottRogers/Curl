---
id: BL-552
title: Write curl's -v and --trace lines for a POP3 session
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-551]
touches: [Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-552 — Write curl's -v and --trace lines for a POP3 session

## Goal

`-v` and `--trace`/`--trace-ascii` on a POP3 transfer write the same lines curl 8.21.0 writes (connect lines, `> ` commands, `< ` replies, STLS TLS lines, closing lines), byte for byte.

## Context

- Conformance audit 2026-09-28, row 34. Events: `ITransferEvents` (ADR-0046); formatting: `Curl.Output.UnitLibrary/VerboseTransferEventWriter.cs`, `TraceTransferEventWriter.cs`.
- Measure with `Record-CurlExchange.ps1 -Pop3`: `-v` for `RETR`, for `USER`/`PASS` (record whether curl masks the password), for `STLS` with `-k`, and `--trace-ascii -` for `RETR`.

## Acceptance criteria

- [x] Measured first as above; stderr and trace output copied into Notes with varying parts marked.
- [x] `Curl.Console.UnitTests` pin the measured `-v` stderr for each case and the `--trace-ascii` dump, normalised as existing `-v` tests do.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

### Measurements (curl 8.21.0 mingw Schannel, 2026-09-28)

`Record-CurlExchange.ps1 -Pop3` default replies and message; URL `pop3://127.0.0.1:<PORT>/1`, `-u user:secret`. `<PORT>` is the recorder's port and `<LOCAL>` curl's ephemeral local port (both vary). As for SMTP (BL-546): every `* ` line ends CRLF, every `< `/`> ` line CR CR LF, and the `Established` line ends in a space.

RETR with AUTH PLAIN, `-sv` (exit 0):
```
*   Trying 127.0.0.1:<PORT>...
* Established connection to 127.0.0.1 (127.0.0.1 port <PORT>) from 127.0.0.1 port <LOCAL> 
< +OK POP3 ready <1896.697170952@localhost>
> CAPA
< +OK Capability list follows
< USER
< SASL PLAIN LOGIN
< STLS
< TOP
< UIDL
< .
> AUTH PLAIN
< + 
> AHVzZXIAc2VjcmV0
< +OK Authenticated
> RETR 1
< +OK 133 octets
{ [24 bytes data]
* Connection #0 to host 127.0.0.1:<PORT> left intact
```
`QUIT` (answered `+OK Bye`) is sent but not shown.

USER/PASS, `-Pop3Reply 'GREETING=+OK POP3 ready','CAPA=+OK\r\nUSER\r\n.'` (exit 0). The password is **not** masked:
```
< +OK POP3 ready
> CAPA
< +OK
< USER
< .
> USER user
< +OK User accepted
> PASS secret
< +OK Logged in
> RETR 1
< +OK 133 octets
{ [24 bytes data]
* Connection #0 to host 127.0.0.1:<PORT> left intact
```
(With the default greeting's timestamp and `CAPA=+OK\r\nUSER\r\n.` curl logs in with `> APOP user a3690e5a521603c99fab34da3f1de37b`, `< +OK Logged in`.)

STLS, `-sv -k --ssl-reqd` (exit 0): after the first CAPA list,
```
> STLS
< +OK Begin TLS negotiation
* schannel: disabled automatic use of client certificate
* schannel: using IP address, SNI is not supported by OS.
* Established connection to 127.0.0.1 (127.0.0.1 port <PORT>) from 127.0.0.1 port <LOCAL> 
> CAPA
< +OK Capability list follows
< USER
< SASL PLAIN LOGIN
< TOP
< UIDL
< .
```
then the same `AUTH PLAIN` ... `left intact` lines as the first case.

`--trace-ascii -` (exit 0), standard output, lines ending a bare LF, the body interleaved as curl writes it:
```
*   Trying 127.0.0.1:<PORT>...
* Established connection to 127.0.0.1 (127.0.0.1 port <PORT>) from 127.0.0.1 port <LOCAL> 
<= Recv header, 43 bytes (0x2b)
0000: +OK POP3 ready <1896.697170952@localhost>
=> Send header, 6 bytes (0x6)
0000: CAPA
<= Recv header, 29 bytes (0x1d)
0000: +OK Capability list follows
<= Recv header, 6 bytes (0x6)
0000: USER
<= Recv header, 18 bytes (0x12)
0000: SASL PLAIN LOGIN
<= Recv header, 6 bytes (0x6)
0000: STLS
<= Recv header, 5 bytes (0x5)
0000: TOP
<= Recv header, 6 bytes (0x6)
0000: UIDL
<= Recv header, 3 bytes (0x3)
0000: .
=> Send header, 12 bytes (0xc)
0000: AUTH PLAIN
<= Recv header, 4 bytes (0x4)
0000: + 
=> Send header, 18 bytes (0x12)
0000: AHVzZXIAc2VjcmV0
<= Recv header, 19 bytes (0x13)
0000: +OK Authenticated
=> Send header, 8 bytes (0x8)
0000: RETR 1
<= Recv header, 16 bytes (0x10)
0000: +OK 133 octets
<= Recv data, 24 bytes (0x18)
0000: From: sender@example.com
From: sender@example.com<= Recv data, 2 bytes (0x2)
0000: 
<CR><LF><= Recv data, 25 bytes (0x19)
...
```
(`<CR><LF>` marks the body's own CRLF on standard output.)
and so on: each body line's text and its CRLF are separate `Recv data` blocks (24/2, 25/2, 17/2, 2, 24/2, 31/2), each followed on standard output by its bytes, then `* Connection #0 to host 127.0.0.1:<PORT> left intact`. Standard error held only the progress meter.

Closing lines, `-sv`:
- `RETR=-ERR no such message` (exit 8): `< -ERR no such message`, `* shutting down connection #0` - no message line (`Weird server reply` is only the exit code's text).
- `RETR=CLOSE` (exit 56): `> RETR 1`, `* response reading failed (errno: 0)`, `* shutting down connection #0`.
- `%01` as the message id (exit 3, after login): `* shutting down connection #0` straight after `< +OK Authenticated`.
- `GREETING=-ERR go away` (exit 8): `* Got unexpected pop3-server response`, `* closing connection #0`.
- `PASS=-ERR denied` (exit 67): `* Access denied. -`, `* closing connection #0`.
- `AUTH=-ERR denied` (exit 67): `< -ERR denied`, `* closing connection #0`.
- 70000-byte CAPA line (exit 100): `> CAPA`, `* closing connection #0` - the partial line is not shown.
- `--login-options BAD` (exit 3): `* closing connection #0` right after `Established`; the greeting is never read.
- Body cut off (`RETR=+OK\r\nabc` with `-Pop3IdleMilliseconds 500`, then the server hangs up; exit 0): `{ [3 bytes data]`, `* Connection #0 ... left intact`.
- LIST (no id): `{ [5 bytes data]` (`1 133`), `left intact`.
- No usable login (no SASL/USER/timestamp, or bearer only): a `* SASL: ...` line then `closing` - left for BL-810.

### What was built

- `Pop3ControlChannel` takes `ITransferEvents`: each command is reported with its CRLF as a request header once written (a failed write reports nothing), each line read with its line end as a response header, skipped lines included. `StopReporting()` silences it; the session calls it before `QUIT`, as `-v` shows neither `QUIT` nor its answer.
- `Pop3BodyDecoder` now writes into `Pop3BodyPieces` (new), which keeps each piece curl's `pop3_write` hands on separately and drops empty ones; `Pop3Session` reports each piece as data received, then writes it, so a `--trace -` dump interleaves with the body exactly as measured.
- `Pop3Session.IsOpen` is set once the login is done. `Pop3ProtocolHandler` ends a success with `left intact`; a failure with its message when `Pop3SessionMessages.IsWrittenByVerbose` says curl writes it (all but the four messages that are only the exit code's text: `Weird server reply`, the URL-format text, `Login denied`, the too-large text), then `shutting down connection #N` once the session was open and `closing connection #N` before. Decided from the measurements above: the split is by phase (logged in or not), not by whether `QUIT` was sent - `RETR=CLOSE` sends no `QUIT` yet shuts down.
- `Pop3ConnectionInfoLines` (new) formats the three closing lines; it mirrors `SmtpConnectionInfoLines`, since protocol libraries may not reference each other.
- `Curl.Output.UnitLibrary` and `Curl.Console` needed no change (BL-546 already made `--trace -` bare-LF once the body goes to standard output).
- The STLS console test pins every line but the three TLS-upgrade lines, which BL-806 adds (its context already names POP3).

### Follow-up

- BL-810: the `* SASL: no auth mechanism was offered or recognized` / `no overlap ...` line before `closing` when no login is possible.

### Results

`dotnet build Curl.slnx -warnaserror`: 0 warnings, 0 errors. Fast tests: all 26 test assemblies passed (Curl.Protocol.Pop3.UnitTests 210, Curl.Console.UnitTests 1398 + 4 skipped). `Measure-CodeQuality.ps1 -Library Curl.Protocol.Pop3.UnitLibrary`: 100% line, 100% branch, 88 members, 0 failing, worst CRAP 10. `dotnet format --verify-no-changes` clean for the three projects changed.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. -v and --trace-ascii on a POP3 transfer write curl 8.21.0's command, response, body-piece and closing lines; STLS TLS lines left to BL-806, no-mechanism SASL line to BL-810
