---
id: BL-050
title: Measure the passed count curl reports when the file:// header output fails
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-021]
touches: [Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-050 — Measure the passed count curl reports when the file:// header output fails

## Goal

A `file://` download whose header output (`-i`, `-D`) stops accepting bytes reports the
same `passed <n> returned 0` count curl 8.21.0 reports, measured rather than assumed.

## Context

BL-021 gave the header-output failure in `FileProtocolHandler.WriteHeadersAsync` the
measured body message, `Failure writing output to destination, passed <n> returned 0`,
with `<n>` the length of the whole synthesised header block (90 bytes for a ten-byte
file). That count is unmeasured. Upstream `lib/file.c` hands each pseudo-header line to
the client writer separately, so curl may report the first line's length instead
(20 for the `Content-Length: 10` line and its CRLF), or buffer the lines and report something else.
Measure it with curl 8.21.0 (for example `curl -D <a pipe that closes early> file:///...`)
before changing anything.

## Acceptance criteria

- [ ] The measured upstream message for a header-output write failure on `file://` is
      recorded in this task's Notes with the exact command that produced it.
- [ ] `ExecuteAsync_HeaderOutputFails_ReportsTheHeaderBlockSize` in
      `Curl.Protocol.File.UnitTests` asserts the measured message (renamed if the count
      is no longer the whole block), and passes.
- [ ] `dotnet test Curl.Protocol.File.UnitTests --filter "TestCategory!=Integration"`
      is green.

## Notes

## Log

- 2026-09-26: Created.
