---
id: BL-1835
title: Close GF-0006 test1284: the Digest probe's Content-Length: 0 replaces a user -H Content-Length
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1799]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1835 — Close GF-0006 test1284: the Digest probe's Content-Length: 0 replaces a user -H Content-Length

## Goal

With `--digest` and a user `-H "Content-Length: n"`, the first POST (the Digest probe, `HttpRequestFraming.AsAuthProbe`, ADR-0441) sends `Content-Length: 0` in place of the user value, as curl 8.21.0 does in upstream test1284.

## Context

Left over from BL-1799 (GF-0006 item `behaviour:test1284`). `HttpRequestHeadFormatter.AppendCustomHeaders` writes the `-H` Content-Length line and `AppendBodyHeaders` then skips its own; a probe framing must drop the custom line and write 0.

## Acceptance criteria

- [ ] A unit test pins the probe sending `Content-Length: 0` and no user Content-Length line.
- [ ] `behaviour:test1284` measures `match` in the next gap run.
- [ ] `dotnet build` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-08: Created.
