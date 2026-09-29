---
id: BL-792
title: Name the Unix socket in HTTP's left-intact line as curl 8.21.0 does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-507]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-792 — Name the Unix socket in HTTP's left-intact line as curl 8.21.0 does

## Goal

Through `--unix-socket <path>`, `-v` ends an HTTP transfer with curl 8.21.0's `Connection #N to host <path lower-cased>:0 left intact`, where it now prints the URL's host and port.

## Context

- Found while doing BL-507 (ADR-0147, Consequences). Measured 2026-09-28 with curl 8.21.0 (mingw, Schannel) and `Record-CurlExchange.ps1 -UnixSocket C:\Users\Public\s.sock`: `-v --unix-socket C:\Users\Public\s.sock http://example.com:8080/x` printed `* Connection #0 to host c:\users\public\s.sock:0 left intact`; ours prints `example.com:8080`.
- The line is built by `Curl.Protocol.Http.UnitLibrary/HttpConnectionInfoLines.cs` from the target's host and port. The handler learns nothing of the socket today: `ConnectResult` (Abstractions) would need to say the connection went through a Unix socket and name its path, which `TcpConnector` (BL-507) knows.
- Check whether the `Reusing existing http: connection with host <host>` line changes too (measured: it names the URL's host, `LocalHost` as typed) and whether a `-L` hop or `--next` group changes it.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -UnixSocket` on the reference build (a path with capitals, and an abstract name on Linux if available); lines copied into Notes.
- [ ] `Curl.Protocol.Http.UnitTests` pins the left-intact line for a Unix socket connection as measured, and the TCP line unchanged.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every library touched.

## Notes

## Log

- 2026-09-28: Created.
