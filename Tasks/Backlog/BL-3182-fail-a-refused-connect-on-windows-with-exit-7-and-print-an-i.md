---
id: BL-3182
title: Fail a refused connect on Windows with exit 7 and print an IPv6 zone-ID host as curl 8.22.0 does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitTests/TcpConnectorRefusedZoneIdTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3182 — Fail a refused connect on Windows with exit 7 and print an IPv6 zone-ID host as curl 8.22.0 does

## Goal

A connect to a refused local port fails at once with exit 7, and the connect error for `http://[::1%25lo]:9/` names the host `::1` as curl 8.22.0 does.

## Context

Found by BL-3173 on 2026-10-22 (Windows). curl 8.22.0 (`C:\Program Files\Git\ucrt64\bin\curl.exe`) with `-sS --connect-timeout 2` against refused port 9 prints `curl: (7) Failed to connect to 127.0.0.1:9 after 0 ms: Could not connect to server` within milliseconds; for `http://[::1%25lo]:9/` it prints `curl: (7) Failed to connect to ::1:9 after 9 ms: Could not connect to server` (the zone id is not shown). Curl instead waits out the connect timeout and prints `curl: (28) Connection timed out after 2002 milliseconds` for every such URL. The message is built in `Curl.Networking.UnitLibrary/TcpConnector.cs` (around the `Failed to connect to {target.Host}:{target.Port}` line); find why a refused dial is not reported as refused on Windows and which host text the target carries for a zone-id URL (`CurlUrl.IdnHost` is `::1%lo`). BL-3173 waits on this task to pin the zone-id connect text.

## Acceptance criteria

- [ ] A unit test in `Curl.Networking.UnitTests/TcpConnectorRefusedZoneIdTests.cs` pins exit 7 and `Failed to connect to ::1:9 after <n> ms: Could not connect to server` for a refused dial of a zone-id IPv6 target.
- [ ] On Windows, `curl -sS http://127.0.0.1:9/` (nothing listening) exits 7 at once, not 28 after the connect timeout.
- [ ] `dotnet build` is clean and the fast tests of `Curl.Networking.UnitTests` pass.

## Notes

## Log

- 2026-10-22: Created.
