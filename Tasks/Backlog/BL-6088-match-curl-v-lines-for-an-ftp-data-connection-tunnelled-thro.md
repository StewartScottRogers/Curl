---
id: BL-6088
title: Match curl -v lines for an FTP data connection tunnelled through a -p proxy
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-6083]
touches: [Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Original.Protocol.Ftp.UnitLibrary/FtpDataConnectEvents.cs, Curl.Original.Protocol.Ftp.UnitTests/FtpProtocolHandlerProxyDataLineTests.cs]
requirement: none
created: 2026-11-27
completed:
---
# BL-6088 — Match curl -v lines for an FTP data connection tunnelled through a -p proxy

## Goal

The `-v` lines of an `ftp -p` data connection through a proxy match real curl, its failure included.

## Context

Measured while doing BL-6083 with a PowerShell loopback proxy that answers the control CONNECT `200`, speaks FTP (`EPSV` `500`, `PASV` `227`) and refuses the data CONNECT `403`: reference curl 8.22.0 writes `* Connecting to example.test port 2000` and, after the refusal line, `* shutting down connection #0`; Curl writes `* Connecting to 127.0.0.1 port 2000` (the proxy's address) and no `shutting down` line. Exit codes and error lines already match.

## Acceptance criteria

- [ ] For the measured exchange, with and without `-f`, Curl's `*` lines from `Connect data stream passively` to the end match reference curl.
- [ ] A focused unit test pins them, `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-27: Created.
