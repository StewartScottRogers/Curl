---
id: BL-2084
title: Fail host 0 as curl does instead of crashing in SystemDnsResolver (upstream test1293)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-11
completed:
---
# BL-2084 — Fail host 0 as curl does instead of crashing in SystemDnsResolver (upstream test1293)

## Goal

Curl fails a transfer to host `0` the way curl 8.21.0 does instead of crashing with an unhandled ArgumentException, so upstream test1293 passes.

## Context

Upstream test1293 runs `http://0 http://HOST:PORT/1293 -F=`: the first URL fails and the second still receives its multipart POST. The Release binary crashes because `SystemDnsResolver.ResolveAsync` (Curl.Networking.UnitLibrary/SystemDnsResolver.cs, the `Dns.GetHostAddressesAsync` lookup near line 55) lets an `ArgumentException` escape for host `0` (the any-address 0.0.0.0, which Dns refuses). Make it fail as curl does: resolve `0` as the literal address 0.0.0.0 and let the connect fail, or return no addresses, whichever gives the exit code and message real curl gives. Read the `<verify>` of 8.21.0 `tests/data/test1293` (no `<errorcode>`, so the run's exit code is that of the last URL) and measure real curl 8.21.0 with `Record-CurlExchange.ps1` for the first URL's own error and message before pinning them. Change nothing outside Curl.Networking.

Source: re-measurement of the gap findings with the fixed gap harness (PR #119, 2026-10-11); parent re-close task BL-2072. Targeted curl version: 8.21.0 (tests/data in the release tarball).

## Acceptance criteria

- [ ] A new MSTest in Curl.Networking.UnitTests (platform-neutral, not an Integration test) shows `SystemDnsResolver.ResolveAsync("0", ...)`, with an injected lookup that throws `ArgumentException` as Dns does, no longer lets the exception escape, and pins the result that makes the transfer fail with the `CurlExitCode` real curl returns for `curl http://0`.
- [ ] The doc comment of `ResolveAsync` states the new behaviour; 100% line and branch coverage of the changed code is kept.
- [ ] Add test1293 to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` once it passes in process.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.

## Notes

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
