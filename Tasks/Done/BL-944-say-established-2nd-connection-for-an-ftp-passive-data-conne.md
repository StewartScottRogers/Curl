---
id: BL-944
title: Say Established 2nd connection for an FTP passive data connection under -v
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-931]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-30
---
# BL-944 — Say Established 2nd connection for an FTP passive data connection under -v

## Goal

An `ftp://` passive data connection's `-v` line reads `* Established 2nd connection to <URL host> (<ip> port <port>) from <ip> port <port> ` as curl 8.21.0 prints it, not `Established connection to <data host> ...`.

## Context

- Found in BL-931 (see its Notes). Measured with curl 8.21.0 (mingw, Schannel): `ftp://127.0.0.1:47931/...` prints `* Established 2nd connection to 127.0.0.1 (127.0.0.1 port 55801) from 127.0.0.1 port 55803 `, and `ftp://localhost:47934/...` prints `* Established 2nd connection to localhost (127.0.0.1 port 56869) from 127.0.0.1 port 56872 `: the URL's host, with "2nd", while ours prints `Established connection to 127.0.0.1 ...` from `TcpConnector` through `ConnectionOpenedEvent` and `TransferEventInfoText`.
- The FTP handler dials the data connection with `ConnectTarget` (`FtpSession.ConnectDataAsync`); one way is a flag or a display host on `ConnectTarget` that `TcpConnector` carries into `ConnectionOpenedEvent`, so `TransferEventInfoText` words it curl's way. BL-931 already reports the active-mode accept's line itself.
- Coordinate with BL-797 and BL-904, which change the same data connect.

## Acceptance criteria

- [x] Measured `-v` for `ftp://127.0.0.1` and `ftp://localhost` copied into Notes.
- [x] A test in `Curl.Output.UnitTests` or `Curl.Networking.UnitTests` pins the `Established 2nd connection to <URL host> (...)` line for a data connection, and the control connection's line is unchanged.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

Measured 2026-09-30 with curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -Ftp -FtpData 'hello ftp\n'`:

`-v ftp://127.0.0.1:47941/dir/file.txt`:
```
*   Trying 127.0.0.1:47941...
* Established connection to 127.0.0.1 (127.0.0.1 port 47941) from 127.0.0.1 port 53193 
* Connecting to 127.0.0.1 port 53192
*   Trying 127.0.0.1:53192...
* Established 2nd connection to 127.0.0.1 (127.0.0.1 port 53192) from 127.0.0.1 port 53194 
* Connection #0 to host 127.0.0.1:47941 left intact
```

`-v ftp://localhost:47942/dir/file.txt`:
```
*   Trying [::1]:47942...
*   Trying 127.0.0.1:47942...
* Established connection to localhost (127.0.0.1 port 47942) from 127.0.0.1 port 53197 
* Connecting to 127.0.0.1 port 53195
*   Trying 127.0.0.1:53195...
* Established 2nd connection to localhost (127.0.0.1 port 53195) from 127.0.0.1 port 53198 
* Connection #0 to host localhost:47942 left intact
```

`-v --disable-epsv --no-ftp-skip-pasv-ip ftp://localhost:47943/dir/file.txt` also prints
`* Established 2nd connection to localhost (127.0.0.1 port 53199) from 127.0.0.1 port 53203 `:
the URL's host, whatever address the data connection dialled.

Decisions (each follows the measurement, so no ADR):
- `ConnectionOpenedEvent.IsSecondConnection` (default `false`) carries the fact;
  `TransferEventInfoText.ConnectionOpened` words it `Established 2nd connection`, so `-v` and
  the trace dumps both follow.
- The FTP data connect's `FtpDataConnectEvents` marks the event and sets its host name to the
  URL's host (`FtpControlConnectionName.Host`), rather than a new `ConnectTarget` field carried
  by `TcpConnector`: the rewrite stays in the FTP library, beside BL-904's failure-line rewrite,
  and `Curl.Networking.UnitLibrary` needs no change.
- `touches` gained `Curl.Protocol.Ftp.UnitLibrary` and `Curl.Protocol.Ftp.UnitTests`: the data
  connect lives there, and no task in Doing named them.
- `Measure-CodeQuality.ps1` flagged the private `FtpProtocolHandler` constructor at complexity 12
  (an untouched `?? throw` per parameter); it now uses `ArgumentNullException.ThrowIfNull`, so
  criterion 4 holds for the FTP library too.

Tests: `VerboseTransferEventWriterTests.SecondConnectionOpened_SaysEstablished2ndConnectionAndTheControlConnectionDoesNot`
(Output) and `FtpProtocolHandlerDataConnectFailureTests.ExecuteAsync_DataConnectionOpened_IsReportedAsTheSecondConnectionToTheUrlsHost` (FTP).
Coverage: Output, Protocol.Abstractions and Protocol.Ftp all 100% line and branch, 0 failing members.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. FTP passive data connections now say Established 2nd connection to <URL host> under -v and trace
