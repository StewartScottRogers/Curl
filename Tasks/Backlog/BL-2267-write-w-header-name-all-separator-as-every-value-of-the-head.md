---
id: BL-2267
title: Write -w %header{name:all:separator} as every value of the header joined by the separator so upstream cases 764 and 765 pass
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-15
completed:
---
# BL-2267 — Write -w %header{name:all:separator} as every value of the header joined by the separator so upstream cases 764 and 765 pass

## Goal

`curl -w '%header{this:all:***}\n'` writes every value of the `this` header, in order, joined by `***` (`one***two***three***four`), as curl 8.22.0 does, so upstream cases 764 and 765 pass.

## Context

BL-2264 (2026-10-15) made the conformance harness report `headers-api`, so 764 and 765 now run and are Inconclusive. Measured: 764 writes an empty line where `one***two***three***four` is expected, and 765 (`%header{this:all:-{\}-}`, a `}` escaped with `\` inside the separator) writes `-}` where `one-{}-two-{}-three-{}-four` is expected. curl's `tool_writeout.c` (`writeOutput`, the `%header{` branch) reads `name:all:sep` and joins every value; `\}` in the separator is a literal `}`. `-w %header{...}` lives in `Curl.Output.UnitLibrary` (`TransferWriteOutVariables.cs`).

## Acceptance criteria

- [ ] A focused unit test in `Curl.Output.UnitTests` pins `%header{name:all:sep}` joining every value, and `\}` inside the separator.
- [ ] 764 and 765 pass (`CURL_UPSTREAM_CASE=764`, Manual category) and are in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] `--ai-help`'s `-w` text stays right if it names `%header{}`.
- [ ] The fast tests are green.

## Notes

## Log

- 2026-10-15: Created.
