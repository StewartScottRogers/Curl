---
id: BL-902
title: Name the control host and via the data host when an FTP data connection fails to connect
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-902 — Name the control host and via the data host when an FTP data connection fails to connect

## Goal

A failed FTP data connection ends with curl 8.21.0's message, which names the control connection's host and port and then the data address: `Failed to connect to <control host>:<control port> via <data host>:<data port> after <n> ms: Could not connect to server`.

## Context

- Found in BL-662. Measured with `Record-CurlExchange.ps1 -Ftp -FtpReply 'EPSV=500 no' 'PASV=227 Entering Passive Mode (127,0,0,2,0,1)'` and `--no-ftp-skip-pasv-ip`: `curl: (7) Failed to connect to 127.0.0.1:47707 via 127.0.0.2:1 after 2041 ms: Could not connect to server`; with an unroutable `10.255.255.1`, exit 28 with the same shape after the SYN timeout; with the default `--ftp-skip-pasv-ip` and port 1, `via 127.0.0.1:1`.
- Today `FtpSession.ConnectDataAsync` passes the connector's message through, which names only the data host. `Curl.Networking.UnitLibrary/TcpConnector.cs` already writes the `via` form for a mapped destination (`ConnectDestination.IsMapped`); find whether the FTP handler can ask for it through `ConnectTarget` (Abstractions) or should rewrite the message itself, and keep the change within FTP if it can.

## Acceptance criteria

- [ ] Measured first, including `-v` for the refused case; stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Ftp.UnitTests` pin the refused (exit 7) and timed-out (exit 28) messages.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-29: Created.
