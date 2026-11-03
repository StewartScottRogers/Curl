---
id: BL-4656
title: Docs: Verbose-Redis-1 and Verbose-Rediss-1 rows name curl 8.22's Established connection line
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4623]
touches: [Documentation/Conformance/Extended/CurlSurface-Transfer-MessagingStreams-Redis.md]
requirement: none
created: 2026-11-02
completed: 2026-11-03
---
# BL-4656 — Docs: Verbose-Redis-1 and Verbose-Rediss-1 rows name curl 8.22's Established connection line

## Goal

The `CurlSurface:Verbose-Redis-1` and `CurlSurface:Verbose-Rediss-1` rows in `Documentation/Conformance/Extended/CurlSurface-Transfer-MessagingStreams.md` name the connection line curl 8.22.0 and Curl write.

## Context

BL-4651 measured real curl 8.22.0 (`Record-CurlExchange.ps1`, `-sv http://localhost:<port>/x`): `-v` writes `* Established connection to localhost (127.0.0.1 port <port>) from 127.0.0.1 port <local>`, not `* Connected to localhost (127.0.0.1) port <port>`. Curl already writes the same line for `redis` and `rediss`; BL-4651 corrected the case's expected prefix in `RedisTransferSurfaceCases.cs` to `* Established connection to localhost (127.0.0.1 port 6379) from `. The doc's two rows (lines 17 and 26) still say `* Connected to localhost (127.0.0.1) port 6379`. The file was held by BL-4623 when BL-4651 ran, hence this task.

## Acceptance criteria

- [x] Both rows say `-v` writes `* Established connection to localhost (127.0.0.1 port 6379) from ...` to standard error (the rediss row keeps its TLS line text); no `Connected to localhost` is left in the file.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: a063ab12 e3a4fd8b.
- 2026-11-03: Doing -> Done. Verbose-Redis-1 and Verbose-Rediss-1 rows name curl 8.22's Established connection line
