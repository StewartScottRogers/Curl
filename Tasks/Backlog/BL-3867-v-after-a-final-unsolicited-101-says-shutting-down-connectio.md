---
id: BL-3867
title: -v after a final unsolicited 101 says shutting down connection where curl 8.22.0 says left intact
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.ConnectionReuse.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3867 — -v after a final unsolicited 101 says shutting down connection where curl 8.22.0 says left intact

## Goal

`curl -v` against a server that answers `HTTP/1.1 101 Switching\r\n\r\nhello` and closes ends
its trace as curl 8.22.0 does: `* Connection #0 to host 127.0.0.1:PORT left intact`.

## Context

Found by BL-3599 (CGR-070, ADR-0824). Since BL-3599 an unsolicited 101 is the final head and
its body runs to the close. curl 8.22.0 (`Record-CurlExchange.ps1`, 2026-10-26) then writes
`sendrecv_dl: we are done` and `Connection #0 to host ... left intact`. Curl still writes
`shutting down connection #N`, because `HttpResponseHeadReader.SwitchedProtocols` keeps
`HttpProtocolHandler.DeliveredWhole` false (ADR-0050). `HttpProtocolHandlerTests.ConnectionReuse.cs`
pins the "101" row of `ExecuteAsync_ResponseDoesNotPersist_LeavesTheConnectionUnmarkedAndShutsItDown`
to the current behaviour. Measure whether curl really reuses the connection for a second URL
(`curl -v URL URL`) before changing the reuse decision, and record what you find in an ADR.

## Acceptance criteria

- [ ] The `-v` trace of the repro above, run through Curl, ends with the same connection line as curl 8.22.0's (measured and recorded in Notes).
- [ ] Whether the connection is reused for a second URL matches curl 8.22.0 (measured), pinned by a unit test.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.
