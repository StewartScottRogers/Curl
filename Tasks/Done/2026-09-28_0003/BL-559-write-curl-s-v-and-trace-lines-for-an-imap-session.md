---
id: BL-559
title: Write curl's -v and --trace lines for an IMAP session
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-558]
touches: [Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-559 — Write curl's -v and --trace lines for an IMAP session

## Goal

`-v` and `--trace`/`--trace-ascii` on an IMAP transfer write the same lines curl 8.21.0 writes (connect lines, `> ` tagged commands, `< ` responses, literal handling, STARTTLS TLS lines, closing lines), byte for byte.

## Context

- Conformance audit 2026-09-28, row 34. Events: `ITransferEvents` (ADR-0046); formatting: `Curl.Output.UnitLibrary/VerboseTransferEventWriter.cs`, `TraceTransferEventWriter.cs`.
- Measure with `Record-CurlExchange.ps1 -Imap`: `-v` for a `UID FETCH`, for `LOGIN` (record whether curl masks the password), for an `APPEND`, for STARTTLS with `-k`, and `--trace-ascii -` for the fetch.

## Acceptance criteria

- [x] Measured first as above; stderr and trace output copied into Notes with varying parts marked.
- [x] `Curl.Console.UnitTests` pin the measured `-v` stderr for each case and the `--trace-ascii` dump, normalised as existing `-v` tests do.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

### Measurements (curl 8.21.0 mingw Schannel, 2026-09-28)

`Record-CurlExchange.ps1 -Imap` default replies and message (100 bytes); `-sv -u user:secret`. `<PORT>` is the recorder's port and `<LOCAL>` curl's ephemeral local port (both vary). As for POP3 (BL-552): every `* ` line ends CRLF, every `< `/`> ` line CR CR LF, the `Established` line ends in a space.

UID FETCH with AUTHENTICATE PLAIN, `imap://127.0.0.1:<PORT>/INBOX;UID=1` (exit 0):
```
*   Trying 127.0.0.1:<PORT>...
* Established connection to 127.0.0.1 (127.0.0.1 port <PORT>) from 127.0.0.1 port <LOCAL> 
< * OK [CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN] ready
> A001 CAPABILITY
< * CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN
< A001 OK CAPABILITY completed
> A002 AUTHENTICATE PLAIN
< + 
> AHVzZXIAc2VjcmV0
< A002 OK Authenticated
> A003 SELECT INBOX
< * FLAGS (\Answered \Flagged \Deleted \Seen \Draft)
< * 2 EXISTS
< * 0 RECENT
< * OK [UIDVALIDITY 1] UIDs valid
< * OK [UIDNEXT 3] Predicted next UID
< A003 OK [READ-WRITE] SELECT completed
> A004 UID FETCH 1 BODY[]
< * 1 FETCH (UID 1 BODY[] {100}
* Found 100 bytes to download
{ [100 bytes data]
* Written 100 bytes, 0 bytes are left for transfer
< )
< A004 OK FETCH completed
* Connection #0 to host 127.0.0.1:<PORT> left intact
```
`LOGOUT` and its answer are sent but not shown.

LOGIN (`GREETING=* OK ready`, `CAPABILITY=* CAPABILITY IMAP4rev1\r\nOK done`): the password is **not** masked - `> A002 LOGIN user secret`, `< A002 OK LOGIN completed`, then the same SELECT/FETCH lines.

APPEND, `-T` a 37-byte file to `/Sent` (exit 0): after the login,
```
> A003 APPEND Sent (\Seen) {37}
< + Ready for literal data
} [37 bytes data]
* upload completely sent off: 37 bytes
> 
< A003 OK APPEND completed
* Connection #0 to host 127.0.0.1:<PORT> left intact
```
An empty file: `{0}`, no data line, `* Request completely sent off`. `APPEND=NO full` (exit 25): `< A003 NO full`, then `left intact` (no message line).

STARTTLS, `-k --ssl-reqd` (exit 0): `> A002 STARTTLS`, `< A002 OK Begin TLS negotiation now`, `* schannel: disabled automatic use of client certificate`, `* schannel: using IP address, SNI is not supported by OS.`, a second `Established connection` line, then `> A003 CAPABILITY` and the rest as above with tags +2.

`--trace-ascii -` (exit 0, standard output, bare LF): each line as `<= Recv header`/`=> Send header` with its CRLF counted (greeting 66 bytes, `A001 CAPABILITY` 17, ...), `* Found 100 bytes to download`, `<= Recv data, 100 bytes (0x64)` with offsets `0000`/`001a`/`0035`/`0048`/`004a`, the message itself, `* Written 100 bytes, 0 bytes are left for transfer`, `<= Recv header, 3 bytes (0x3)` `)`, `<= Recv header, 25 bytes (0x19)` `A004 OK FETCH completed`, `left intact`. Pinned whole in `CurlCommandRunnerImapTransferEventTests`.

`-X "FETCH 1 BODY[]"` on `/INBOX`: `< * 1 FETCH (BODY[] {100}`, `* Found 100 bytes to download`, the line as data (`{ [25 bytes data]`, `Recv data, 25 bytes` in the trace), the literal as `Recv data, 100 bytes`, then `left intact` - no `Written` line, and `)` and the completion are never read.

Closing lines, `-sv`:
- `LOGIN=NO denied` (exit 67): `* Access denied. \x02`, `* closing connection #0`.
- `SELECT=NO ...` (exit 67): `* Select failed`, `* shutting down connection #0`.
- `UID FETCH=NO gone` (exit 78): `* shutting down connection #0` - no message.
- `UID FETCH=CLOSE` (exit 56): `* response reading failed (errno: 0)`, `* shutting down connection #0`.
- `GREETING=* BYE go away` (exit 8): `* Got unexpected imap-server response`, `* closing connection #0`.
- FETCH completion `NO odd` after the literal (exit 8): `< A004 NO odd`, `left intact`.
- Literal cut short (`{100}` then 12 bytes and a hang-up, exit 18): `{ [12 bytes data]`, `* Written 12 bytes, 88 bytes are left for transfer`, `{ [0 bytes data]`, `* end of response with 88 bytes missing`, `* closing connection #0`.
- `GREETING=* PREAUTH welcome`: `* PREAUTH connection, already authenticated` straight after the greeting.

### What was built

- `ImapControlChannel` takes `ITransferEvents`: each line sent is reported with its CRLF as a request header and each `APPEND` literal piece as data sent, once written (a failed write reports nothing); each line read with its LF as a response header; a literal inside a wanted untagged response is reported split at each LF, as curl's line reader splits it. `StopReporting()` silences it before `LOGOUT`. Its read buffer is 900 bytes, curl's pingpong reader's, so the part of a `FETCH` literal that arrives with its response line matches curl's `Written` line.
- `ImapSession` reports `Found N bytes to download`, each literal piece as data received before it is written, `Written N bytes, M bytes are left for transfer` for the piece that came with the response line (FETCH only), the PREAUTH line, and tracks `ImapSessionPhase` (new). `ImapAppend` reports `upload completely sent off` / `Request completely sent off`.
- `ImapProtocolHandler` ends with `left intact` for a success or a failure once the literal was through; else a failure's message when `ImapSessionMessages.IsWrittenByVerbose` says curl writes one, then `shutting down connection #N` once logged in and `closing connection #N` before logging in or inside a literal. `ImapInfoLines` (new) formats the lines.
- Decided from the `-X` trace (this run): a listed literal's pieces are reported as data too, with no progress and no `Written` line; the earlier draft reported none.
- `Curl.Output.UnitLibrary` needed no change.
- The STARTTLS console test pins every line but the three TLS-upgrade lines, which BL-806 adds, as BL-552 did for POP3.

### Results

`dotnet build Curl.slnx -warnaserror`: 0 errors. Fast tests: every assembly passed (Curl.Protocol.Imap.UnitTests 333, Curl.Console.UnitTests 1403 + 4 skipped). `Measure-CodeQuality.ps1 -Library Curl.Protocol.Imap.UnitLibrary`: 100% line, 100% branch, 140 members, 0 failing, worst CRAP 10. `dotnet format --verify-no-changes` clean for the three projects changed.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. -v and --trace-ascii on an IMAP transfer write curl 8.21.0's command, response, literal, upload and closing lines; STARTTLS TLS lines left to BL-806
