---
id: BL-2470
title: Engine: send curl_mime_data_cb parts and non-UTF-8 curl_mime_data bytes in a MIMEPOST
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2439]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-16
completed:
---
# BL-2470 — Engine: send curl_mime_data_cb parts and non-UTF-8 curl_mime_data bytes in a MIMEPOST

## Goal

A perform with `CURLOPT_MIMEPOST` sends a `curl_mime_data_cb` part (`EasyMimeCallback`) by calling its read callback (and its seek callback on a resend), and sends `curl_mime_data` bytes (`EasyMimeBytes`) verbatim, whatever bytes they hold, byte-identical to libcurl.

## Context

BL-2439 built `IMimeBuilder` and sent MIMEPOST through `Curl.Engine`'s `MimePostForm`, which maps parts with `MultipartFormPartMapping.FromMimeBody` onto `Curl.Core`'s `MultipartFormPart`. That record carries text (a `string`, re-encoded by the builder's text encoding, UTF-8 in the engine) and file paths only, so a callback part throws `NotSupportedException` from `FromMimeBody` and bytes that are not valid UTF-8 are altered. Give `MultipartFormPart` a raw-bytes content and a read-callback content (a `Stream` factory or `Func<Stream>` is enough; `Curl.Core` must not reference `Curl.Api`), make `MultipartFormBodyBuilder` send them (an unknown size, -1, makes the body chunked as libcurl does), and map `EasyMimeBytes` and `EasyMimeCallback` onto them in `MultipartFormPartMapping`. The tool's `-F` path keeps its text behaviour (its text is command-line text in the platform encoding). A callback returning `CURL_READFUNC_ABORT` ends the perform with `AbortedByCallback`.

## Acceptance criteria

- [ ] An engine test performs a MIMEPOST whose part holds the bytes 0x00-0xFF and asserts they are sent unchanged.
- [ ] An engine test performs a MIMEPOST with a `curl_mime_data_cb` part of known size and one of unknown size (-1) and asserts the request bytes (Content-Length vs chunked) against libcurl's layout.
- [ ] `MultipartFormPartMapping.FromMimeBody` no longer throws for `EasyMimeCallback`.
- [ ] `dotnet build` is clean and the fast tests pass; `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary,Curl.Engine.UnitLibrary` reports 0 failing members.
- [ ] `Curl.Engine.UnitLibrary/CLAUDE.md`'s "The MIME API (BL-2439)" section no longer says these parts cannot be sent.
## Notes

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing.
