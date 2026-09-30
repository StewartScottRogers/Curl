---
id: BL-944
title: Say Established 2nd connection for an FTP passive data connection under -v
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-931]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Protocol.Abstractions.UnitLibrary]
requirement: none
created: 2026-09-29
completed:
---
# BL-944 — Say Established 2nd connection for an FTP passive data connection under -v

## Goal

An `ftp://` passive data connection's `-v` line reads `* Established 2nd connection to <URL host> (<ip> port <port>) from <ip> port <port> ` as curl 8.21.0 prints it, not `Established connection to <data host> ...`.

## Context

- Found in BL-931 (see its Notes). Measured with curl 8.21.0 (mingw, Schannel): `ftp://127.0.0.1:47931/...` prints `* Established 2nd connection to 127.0.0.1 (127.0.0.1 port 55801) from 127.0.0.1 port 55803 `, and `ftp://localhost:47934/...` prints `* Established 2nd connection to localhost (127.0.0.1 port 56869) from 127.0.0.1 port 56872 `: the URL's host, with "2nd", while ours prints `Established connection to 127.0.0.1 ...` from `TcpConnector` through `ConnectionOpenedEvent` and `TransferEventInfoText`.
- The FTP handler dials the data connection with `ConnectTarget` (`FtpSession.ConnectDataAsync`); one way is a flag or a display host on `ConnectTarget` that `TcpConnector` carries into `ConnectionOpenedEvent`, so `TransferEventInfoText` words it curl's way. BL-931 already reports the active-mode accept's line itself.
- Coordinate with BL-797 and BL-904, which change the same data connect.

## Acceptance criteria

- [ ] Measured `-v` for `ftp://127.0.0.1` and `ftp://localhost` copied into Notes.
- [ ] A test in `Curl.Output.UnitTests` or `Curl.Networking.UnitTests` pins the `Established 2nd connection to <URL host> (...)` line for a data connection, and the control connection's line is unchanged.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-29: Created.
