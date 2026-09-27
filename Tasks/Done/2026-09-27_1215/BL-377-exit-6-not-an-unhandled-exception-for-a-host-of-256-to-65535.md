---
id: BL-377
title: Exit 6, not an unhandled exception, for a host of 256 to 65535 bytes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed: 2026-09-27
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

- [x] A `SystemDnsResolver` unit test pins that a host longer than 255 characters is reported as not resolved, with no exception.
- [x] The stderr line and exit 6 match curl 8.21.0 for a 300-byte host, measured and recorded in the test's comment.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Measured 2026-09-27, curl 8.21.0 (mingw, Schannel): `curl -sS http://<300 a's>/` exits 6 with
  `curl: (6) Could not resolve host: ` and the first 231 `a`s; a 250-byte host gives the same 231, so
  the cut is curl's 256-byte error buffer on the whole message. `-x http://<300 a's>:3128` exits 5 with
  230 `a`s after `Could not resolve proxy: `.
- `SystemDnsResolver` catches the `ArgumentOutOfRangeException` `Dns` throws for a name over 255
  characters and returns no addresses (tests `ResolveAsync_WithHostLongerThan255Characters_...` with
  256, 300 and 65535, offline through the real `Dns`).
- The resolve messages in `TcpConnector` (host and proxy), `UdpDatagramConnector` and
  `SocksProxyTunnel.CouldNotResolve` are cut to 255 characters by the new internal `CurlErrorBuffer`
  (ADR-0072). The general cut belongs in `Curl.Console`, which BL-329 held, so it is filed as BL-380.
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0072; no task in Doing names it.
- End to end: our `curl.exe -sS http://<300 a's>/` and curl 8.21.0 wrote byte-identical stderr, both exit 6.
- Pipeline: the feature stages were run in the session rather than through subagents; the change is
  one catch, one internal helper and four call sites.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. A host of 256 to 65535 bytes exits 6 with curl 8.21.0's Could not resolve host: line cut to 255 bytes, byte-identical to curl, instead of an unhandled exception
