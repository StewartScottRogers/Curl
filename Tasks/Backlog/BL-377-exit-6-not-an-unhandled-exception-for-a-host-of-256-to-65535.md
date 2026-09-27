---
id: BL-377
title: Exit 6, not an unhandled exception, for a host of 256 to 65535 bytes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-377 — Exit 6, not an unhandled exception, for a host of 256 to 65535 bytes

## Goal

`curl http://<300 a's>/` fails with exit 6 (`CURLE_COULDNT_RESOLVE_HOST`) and curl's `Could not resolve host:` line, as curl 8.21.0 does, instead of crashing with an unhandled `ArgumentOutOfRangeException`.

## Context

- Found under BL-327 (2026-09-27): a 65535-byte host (the longest curl 8.21.0 accepts) reaches
  `Curl.Networking.UnitLibrary/SystemDnsResolver.cs` `ResolveAsync`, and `Dns.GetHostAddressesAsync`
  throws `ArgumentOutOfRangeException` ("cannot be longer than 255 characters") for any name over 255
  characters; the exception escapes `Program.Main`.
- curl 8.21.0 (Schannel) on the same 65535-byte host exits 6 (measured 2026-09-27). Measure the stderr
  line for a 300-byte host before pinning it.

## Acceptance criteria

- [ ] A `SystemDnsResolver` unit test pins that a host longer than 255 characters is reported as not resolved, with no exception.
- [ ] The stderr line and exit 6 match curl 8.21.0 for a 300-byte host, measured and recorded in the test's comment.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-09-27: Created.
