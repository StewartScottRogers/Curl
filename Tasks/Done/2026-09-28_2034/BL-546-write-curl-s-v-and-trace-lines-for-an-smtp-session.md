---
id: BL-546
title: Write curl's -v and --trace lines for an SMTP session
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-545]
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-546 — Write curl's -v and --trace lines for an SMTP session

## Goal

`-v` and `--trace`/`--trace-ascii` on an SMTP transfer write the same lines curl 8.21.0 writes: the connect lines, each command as `> ` and each reply line as `< `, the TLS lines for STARTTLS, and the closing lines, byte for byte.

## Context

- Conformance audit 2026-09-28, row 34.
- Handlers report through `ITransferEvents` (ADR-0046); `Curl.Output.UnitLibrary/VerboseTransferEventWriter.cs` and `TraceTransferEventWriter.cs` format them. Follow how the FTP handler reports its control-connection lines.
- Measure with `Record-CurlExchange.ps1 -Smtp`: `-v` for an upload, for `AUTH PLAIN` (curl may mask credentials; record what it prints), for STARTTLS with `-k`, and `--trace-ascii -` for the upload.

## Acceptance criteria

- [x] Measured first as above; stderr (and the trace output) copied into Notes byte for byte, with the parts that vary (ports, times) marked.
- [x] `Curl.Console.UnitTests` pin the measured `-v` stderr for each case through fake connectors, with varying parts normalised as existing `-v` tests do.
- [x] A `--trace-ascii` test pins the measured dump for the upload.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

### Measurements (curl 8.21.0 mingw Schannel, 2026-09-28)

`Record-CurlExchange.ps1 -Smtp` default replies; `mail.txt` = `Subject: hi CRLF CRLF Hello CRLF` (22 bytes), given by absolute path; URL `smtp://127.0.0.1:<PORT>/client`. `<PORT>` is the recorder's port and `<LOCAL>` curl's ephemeral local port (both vary). Every `* ` line ends CRLF; every `< `/`> ` line ends CR CR LF (the header's own CRLF, then text mode). The `Established` line ends in a space.

Upload, `-sv --mail-from a@b --mail-rcpt c@d -T mail.txt` (exit 0):
```
*   Trying 127.0.0.1:<PORT>...
* Established connection to 127.0.0.1 (127.0.0.1 port <PORT>) from 127.0.0.1 port <LOCAL> 
< 220 localhost ESMTP
> EHLO client
< 250-localhost
< 250-AUTH PLAIN LOGIN CRAM-MD5
< 250-STARTTLS
< 250-SIZE 1000000
< 250-8BITMIME
< 250 SMTPUTF8
> MAIL FROM:<a@b> SIZE=22
< 250 OK
> RCPT TO:<c@d>
< 250 OK
> DATA
< 354 End data with <CR><LF>.<CR><LF>
} [25 bytes data]
* upload completely sent off: 25 bytes
< 250 OK message accepted
* Connection #0 to host 127.0.0.1:<PORT> left intact
```
The transcript shows curl did send `QUIT` (answered `221 Bye`) after the message, but `-v` shows neither.

AUTH PLAIN, `-sv -u user:secret`, `-SmtpReply "EHLO=250-localhost\r\n250 AUTH PLAIN"` (exit 0). Credentials are **not** masked:
```
*   Trying 127.0.0.1:<PORT>...
* Established connection to 127.0.0.1 (127.0.0.1 port <PORT>) from 127.0.0.1 port <LOCAL> 
< 220 localhost ESMTP
> EHLO client
< 250-localhost
< 250 AUTH PLAIN
> AUTH PLAIN
< 334 
> AHVzZXIAc2VjcmV0
< 235 Authentication successful
> MAIL FROM:<a@b>
< 250 OK
> RCPT TO:<c@d>
< 250 OK
> DATA
< 354 End data with <CR><LF>.<CR><LF>
} [25 bytes data]
* upload completely sent off: 25 bytes
< 250 OK message accepted
* Connection #0 to host 127.0.0.1:<PORT> left intact
```
(With the default EHLO reply curl picks CRAM-MD5: `> AUTH CRAM-MD5`, `< 334 PDE4OTYuNjk3MTcwOTUyQGxvY2FsaG9zdD4=`, `> dXNlciA0ZWY4ZWU0NDBiYjU0MTg5NWEwMWY2OWZiMjY2MjlkZA==`, `< 235 Authentication successful`.)

STARTTLS, `-v -k --ssl-reqd` (exit 0; measured without `-s`, so progress-meter fragments were interleaved and are left out here). After `< 250 SMTPUTF8` of the first EHLO reply:
```
> STARTTLS
< 220 Ready to start TLS
* schannel: disabled automatic use of client certificate
* schannel: using IP address, SNI is not supported by OS.
* Established connection to 127.0.0.1 (127.0.0.1 port <PORT>) from 127.0.0.1 port <LOCAL> 
> EHLO client
< 250-localhost
< 250-AUTH PLAIN LOGIN CRAM-MD5
< 250-SIZE 1000000
< 250-8BITMIME
< 250 SMTPUTF8
```
then the same `MAIL`…`left intact` lines as the upload.

`--trace-ascii - --mail-from a@b --mail-rcpt c@d -T mail.txt` (exit 0), standard output, every line ending a bare LF:
```
*   Trying 127.0.0.1:<PORT>...
* Established connection to 127.0.0.1 (127.0.0.1 port <PORT>) from 127.0.0.1 port <LOCAL> 
<= Recv header, 21 bytes (0x15)
0000: 220 localhost ESMTP
=> Send header, 13 bytes (0xd)
0000: EHLO client
<= Recv header, 15 bytes (0xf)
0000: 250-localhost
<= Recv header, 31 bytes (0x1f)
0000: 250-AUTH PLAIN LOGIN CRAM-MD5
<= Recv header, 14 bytes (0xe)
0000: 250-STARTTLS
<= Recv header, 18 bytes (0x12)
0000: 250-SIZE 1000000
<= Recv header, 14 bytes (0xe)
0000: 250-8BITMIME
<= Recv header, 14 bytes (0xe)
0000: 250 SMTPUTF8
=> Send header, 25 bytes (0x19)
0000: MAIL FROM:<a@b> SIZE=22
<= Recv header, 8 bytes (0x8)
0000: 250 OK
=> Send header, 15 bytes (0xf)
0000: RCPT TO:<c@d>
<= Recv header, 8 bytes (0x8)
0000: 250 OK
=> Send header, 6 bytes (0x6)
0000: DATA
<= Recv header, 37 bytes (0x25)
0000: 354 End data with <CR><LF>.<CR><LF>
=> Send data, 25 bytes (0x19)
0000: Subject: hi
000d: 
000f: Hello
0016: .
* upload completely sent off: 25 bytes
<= Recv header, 25 bytes (0x19)
0000: 250 OK message accepted
* Connection #0 to host 127.0.0.1:<PORT> left intact
```
Standard error held only the progress meter.

Closing lines, `-sv`: `RCPT=550 no such user` (exit 55) ends `< 550 no such user`, `* RCPT failed: 550`, `* shutting down connection #0`; `GREETING=554 go away` (exit 8) ends `< 554 go away`, `* Got unexpected smtp-server response: 554`, `* closing connection #0`; no `-T` (`VRFY c@d`, exit 0) ends `< 250 Recorder <recorder@localhost>`, `{ [35 bytes data]`, `* Connection #0 to host 127.0.0.1:<PORT> left intact`.

### What was built

- `SmtpControlChannel` reports each command (with CRLF) as a request header after it is written, each line read (with its line end, skipped lines too) as a response header, and message bytes as data sent. A failed write reports nothing. `QuitAsync` stops reporting before it sends `QUIT`, since curl sends it at cleanup where `-v` does not see it, and records `QuitSent`.
- `SmtpMailTransaction` sends the last piece of the message together with the end-of-data mark, so the 25-byte upload is one send and one data event as in curl's dump (and one progress report; `SmtpProtocolHandlerUploadTests` updated from two reports to one), then reports `upload completely sent off: N bytes`.
- `SmtpCommandTransfer` reports each reply written to the output as data received.
- `SmtpProtocolHandler` ends the transfer with `Connection #N to host H:P left intact` on success; on failure with the error message, then `shutting down connection #N` when `QUIT` was sent and `closing connection #N` when not. The rule is inferred from the two measured failures (RCPT refused sends `QUIT`; a refused greeting does not) - the same rule as curl's `Curl_conncontrol` shutdown/close split.
- `Curl.Console` added to `touches` (no task in Doing names it): measured `--trace-ascii -` output ends lines in a bare LF when the transfer's body goes to standard output, because curl switches standard output to binary mode as such a transfer starts; with `-o` it stays text mode (CR LF, BL-242's measurement). The runner now sets `standardOutputSwitchedToBinary` at the start of `TransferAsync` as well as after it, and a `--trace -` dump on Windows writes through the new `TextModeUntilBinaryStream`, which reads that flag at each write. Write-out and cookie-jar behaviour are unchanged: they read the flag after the transfer, when the old code had already set it.
- `Curl.Output.UnitLibrary` needed no change: its writers already render headers, data and info lines as measured.

### Left for BL-806

The three lines curl writes during the STARTTLS upgrade (two `schannel:` lines and a second `Established connection` line) need `ITlsProvider` to carry `ITransferEvents` (Abstractions and Networking, not this task's projects), and the `schannel:` lines are not written after an HTTPS connect either. `CurlCommandRunnerSmtpTransferEventTests.RunAsync_VerboseMailUploadWithStartTls_...` pins every other line and names BL-806 for these three. Filed as BL-806 rather than widening this task.

### Results

`dotnet build Curl.slnx -warnaserror`: 0 warnings, 0 errors. Fast tests: all 26 test assemblies passed (Curl.Protocol.Smtp.UnitTests 191, Curl.Console.UnitTests 1395 before the 4 `TextModeUntilBinaryStreamTests`). `Measure-CodeQuality.ps1`: Curl.Protocol.Smtp.UnitLibrary 100/100, 93 members, 0 failing, worst CRAP 10; Curl.Console 100/100, 577 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. -v and --trace-ascii on an SMTP transfer write curl 8.21.0's command, reply, data and closing lines; STARTTLS TLS lines left to BL-806
