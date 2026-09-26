---
id: BL-261
title: Select sws reply parts for authentication, swsbounce and CONNECT in the sws emulation
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-146]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-261 — Select sws reply parts for authentication, swsbounce and CONNECT in the sws emulation

## Goal

`SwsHttpServerConnector` picks the reply part for authenticated, bounced and CONNECT requests
the way sws does, so HTTP authentication and proxy cases get the reply upstream serves.

## Context

- BL-146 selects `<data>` / `<dataN>` from the path only (`SwsHttpReplySelector`).
- Upstream: `tests/server/sws.c` at `curl-8_21_0`, `sws_ProcessRequest`, `sws_send_doc` and
  `service_connection`:
  - `Authorization: Digest` adds 1000 to the part number once per request; NTLM type-1
    (`TlRMTVNTUAAB`) adds 1001 and type-3 (`TlRMTVNTUAAD`) adds 1002; `Authorization: Basic`
    with a part already at 1000 or more adds 1; `Authorization: Negotiate` counts up from the
    previous part.
  - A reply containing `swsbounce` makes the next request for the same test get the previous
    part number plus one (state kept across connections).
  - A `CONNECT host:port HTTP/x.y` request is answered from `<connect>` / `<connectN>`.

## Acceptance criteria

- [ ] Tests show each rule above selects the part sws selects (Digest to `<data1000>`, NTLM
      type-1 to `<data1001>`, type-3 to `<data1002>`, Basic after 1000 to `<data1001>`,
      Negotiate counting up, swsbounce to part + 1, CONNECT to `<connect>`).
- [ ] 100% line and branch coverage of `Curl.Conformance.UnitLibrary`, complexity at most 10,
      per `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-09-26: Created.
