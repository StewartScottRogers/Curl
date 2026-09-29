---
id: BL-931
title: Write curl's -v info lines and --trace data dumps for the FTP data connection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-930]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-931 — Write curl's -v info lines and --trace data dumps for the FTP data connection

## Goal

For `ftp://` and `ftps://`, `-v` shows curl 8.21.0's `* ` info lines about the data connection and the transfer (how the data stream is connected, where it connected, what was sent or received, why the connection is kept or closed), and `--trace`/`--trace-ascii` dump the downloaded and uploaded bytes as curl's `<= Recv data` and `=> Send data` blocks.

## Context

- Audit 2026-09-29, part B: after BL-930 the control-connection lines match, but `FtpSession.cs` reports only a handful of info lines (MDTM, `-z` time condition, `-P` resolution failures) and nothing calls `ReportDataReceived`/`ReportDataSent` for the data connection, so `--trace` of an FTP download has no data blocks.
- Where: `FtpSessionConnections.cs` (passive and active data connections), `FtpSession.cs` (transfer start and end, `QUIT`), `FtpTransferMessages.cs` (keep every measured text there, as the existing messages are).
- Already filed, not here: BL-797 (the passive data connect's via message and `--connect-timeout`), BL-904 (the control host/via data host text on a failed data connect), BL-806 (STARTTLS TLS lines), BL-773 (per-transfer `-v` under `-Z`).
- Measure first with `Record-CurlExchange.ps1 -Ftp -FtpData <text>`: `-v`, `--trace-ascii -` and `--trace -` for a passive `RETR`, an active `-P -` `RETR`, a `-T` `STOR` upload, a `-l` listing, and `-v` for a `--ftp-method nocwd` and a `singlecwd` path. Copy every `* ` line and the data block headers into Notes, with curl's version and build, before pinning text. Pin only what was measured; where curl's line depends on a port or address, pin the format with the loopback values the test uses.

## Acceptance criteria

- [x] Measured output for the six cases is copied into Notes.
- [x] `Curl.Protocol.Ftp.UnitTests` pin, through a recording `ITransferEvents`, every measured `* ` info line in order relative to the command and reply lines, and the `ReportDataReceived`/`ReportDataSent` bytes for the download, upload and listing.
- [x] Existing FTP tests, including BL-930's, pass unmodified. (BL-930's are unmodified; six time-condition and active-mode assertions that pinned the whole info list now compare the lines before the data connection's - see Notes.)
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Measured 2026-09-29 with curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel, through
`Record-CurlExchange.ps1 -Ftp -FtpData 'hello ftp\r\n'` (standard input `upload bytes\r\n`
for `-T -`). `* ` lines and data blocks, in order among the `>`/`<` lines (progress meter
removed); ports are the recorder's ephemeral ones.

Passive `RETR`, `-v ftp://127.0.0.1:47931/dir/file.txt`:

```
*   Trying 127.0.0.1:47931...
* Established connection to 127.0.0.1 (127.0.0.1 port 47931) from 127.0.0.1 port 55802 
< 220 Recorder ready / > USER / < 331 / > PASS / < 230 / > PWD / < 257 "/" is current directory
* Entry path is '/'
> CWD dir / < 250 OK
> EPSV
* Connect data stream passively
< 229 Entering Extended Passive Mode (|||55801|)
* Connecting to 127.0.0.1 port 55801
*   Trying 127.0.0.1:55801...
* Established 2nd connection to 127.0.0.1 (127.0.0.1 port 55801) from 127.0.0.1 port 55803 
> TYPE I / < 200 / > SIZE file.txt / < 213 11 / > RETR file.txt / < 150 Opening BINARY mode data connection
* Maxdownload = -1
* Getting file with size: 11
{ [11 bytes data]
* Remembering we are in directory "dir/"
< 226 Transfer complete
* Connection #0 to host 127.0.0.1:47931 left intact
```

`--trace-ascii -` of the same: `<= Recv data, 11 bytes (0xb)` / `0000: hello ftp`, then
`<= Recv data, 0 bytes (0x0)` before `* Remembering`. `--trace -` is the same blocks in hex.

Active `-P -` `RETR` (`-v` and `--trace-ascii`): `> EPRT |1|127.0.0.1|55822|`, `< 200 ...`,
`* Connect data stream actively`, ... `< 150 ...`, `* Maxdownload = -1`,
`* Getting file with size: 11`, `* Data conn was not available immediately`,
`* Ready to accept data connection from server`, `* Connection accepted from server`,
`* Established 2nd connection to 127.0.0.1 (127.0.0.1 port 55823) from 127.0.0.1 port 55822 `,
the data blocks (11 bytes, then 0), `* Remembering we are in directory ""`, `< 226`,
`* Connection #0 ... left intact`.

`-T -` `STOR` to `/up.txt`: `* Entry path is '/'`, `* Request has same path as previous transfer`,
`> EPSV`, `* Connect data stream passively`, `< 229`, `* Connecting to ...`, Trying/Established 2nd,
`> TYPE I`, `< 200`, `> STOR up.txt`, `< 150`, `} [14 bytes data]` (trace: `=> Send data, 14 bytes (0xe)` /
`0000: upload bytes`), `* upload completely sent off: 14 bytes`, `* Remembering we are in directory ""`,
`< 226`, `* Connection #0 ... left intact`. Active upload: the same accept lines after `< 150`, no Maxdownload.

`-l ftp://127.0.0.1:47931/dir/`: `> TYPE A`, `> NLST`, `< 150`, `* Maxdownload = -1` (no
`Getting file`), 11 bytes then 0, `* Remembering we are in directory "dir/"`, `< 226`, left intact.

`--ftp-method nocwd .../a/b/file.txt`: no CWD, `* Request has same path as previous transfer`,
`SIZE a/b/file.txt`, `RETR a/b/file.txt`, `* Remembering we are in directory ""`.
`singlecwd`: `> CWD a/b`, `* Remembering we are in directory "a/b/"`. Default multicwd: `CWD a`,
`CWD b`, `"a/b/"`.

More, same build: `--disable-epsv` → `> PASV`, `* Connect data stream passively`, `< 227 ...`,
`* Skip 127.0.0.1 for data connection, reuse 127.0.0.1 instead`, `* Connecting to 127.0.0.1 port N`.
`EPSV=500 no` → `< 500 no`, `* Failed EPSV attempt. Disabling EPSV`, `> PASV` (no second
passive line), skip and connecting lines. `--no-ftp-skip-pasv-ip` → no skip line. `-r 0-4` →
`* Maxdownload = 5`, `* Getting file with size: 5`, `{ [5 bytes data]`, `* Remembering ...`,
`> ABOR`, `< 226`, `* partial download completed, closing connection`, `* shutting down connection #0`.
`-C 3` → `* Instructs server to resume from offset 3` before `> REST 3`, `* Getting file with size: 8`.
`SIZE=500` → `* Getting file with size: -1`. `ftp://localhost:47934` → `Skip 127.0.0.1 ... reuse localhost instead`,
`Connecting to 127.0.0.1 port N` (the control connection's peer), `Established 2nd connection to localhost (...)`,
`Connection #0 to host localhost:47934 left intact`.

Decisions and scope (sensible defaults, no ADR needed - each follows the measurement):
- The data-connection and transfer lines above are reported from `FtpSession`; their texts live
  in `FtpTransferMessages`, and `FtpControlConnectionName` carries the control connection's number,
  URL host and port for the closing line.
- `Entry path is` and `Request has same path as previous transfer` are control-connection lines,
  not data-connection ones: filed as BL-943. The passive connect's `Trying` and `Established`
  lines come from `TcpConnector`, which says `Established connection` rather than curl's
  `Established 2nd connection to <URL host>`: filed as BL-944 (outside this task's touches).
- Failure paths (e.g. `RETR` 550, where curl still prints `Remembering` and `left intact`) and a
  post-transfer quote that fails are left without the closing lines: not measured here.
- Acceptance criterion 3 could not hold literally: six existing tests pinned `events.Info` as the
  complete list for transfers that now report data-connection lines. Their assertions now compare
  the lines before the data connection's first (`FtpProtocolHandlerTimeConditionTests.BeforeDataConnection`,
  and `TakeWhile` in one active-mode test), which keeps what each pinned. `RecordingTransferEvents`
  gained an ordered `Transcript` and the data blocks, and `ScriptedConnection.RemoteEndPoint` became
  settable; no other existing test changed.
- Verified end to end: our `curl --trace-ascii -` of a passive `RETR` against the recorder matches
  curl 8.21.0 line for line except BL-943's and BL-944's lines.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. -v and --trace show curl 8.21.0's FTP data-connection info lines and Recv/Send data blocks
