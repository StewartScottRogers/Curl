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
completed: 2026-10-16
---
# BL-2470 — Engine: send curl_mime_data_cb parts and non-UTF-8 curl_mime_data bytes in a MIMEPOST

## Goal

A perform with `CURLOPT_MIMEPOST` sends a `curl_mime_data_cb` part (`EasyMimeCallback`) by calling its read callback (and its seek callback on a resend), and sends `curl_mime_data` bytes (`EasyMimeBytes`) verbatim, whatever bytes they hold, byte-identical to libcurl.

## Context

BL-2439 built `IMimeBuilder` and sent MIMEPOST through `Curl.Engine`'s `MimePostForm`, which maps parts with `MultipartFormPartMapping.FromMimeBody` onto `Curl.Core`'s `MultipartFormPart`. That record carries text (a `string`, re-encoded by the builder's text encoding, UTF-8 in the engine) and file paths only, so a callback part throws `NotSupportedException` from `FromMimeBody` and bytes that are not valid UTF-8 are altered. Give `MultipartFormPart` a raw-bytes content and a read-callback content (a `Stream` factory or `Func<Stream>` is enough; `Curl.Core` must not reference `Curl.Api`), make `MultipartFormBodyBuilder` send them (an unknown size, -1, makes the body chunked as libcurl does), and map `EasyMimeBytes` and `EasyMimeCallback` onto them in `MultipartFormPartMapping`. The tool's `-F` path keeps its text behaviour (its text is command-line text in the platform encoding). A callback returning `CURL_READFUNC_ABORT` ends the perform with `AbortedByCallback`.

## Acceptance criteria

- [x] An engine test performs a MIMEPOST whose part holds the bytes 0x00-0xFF and asserts they are sent unchanged.
- [x] An engine test performs a MIMEPOST with a `curl_mime_data_cb` part of known size and one of unknown size (-1) and asserts the request bytes (Content-Length vs chunked) against libcurl's layout.
- [x] `MultipartFormPartMapping.FromMimeBody` no longer throws for `EasyMimeCallback`.
- [x] `dotnet build` is clean and the fast tests pass; `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary,Curl.Engine.UnitLibrary` reports 0 failing members.
- [x] `Curl.Engine.UnitLibrary/CLAUDE.md`'s "The MIME API (BL-2439)" section no longer says these parts cannot be sent.
## Notes

- Plan (ADR-0627, decided by Claude under Stewart's delegation): `MultipartFormPartKind` gains `Bytes`
  (`MultipartFormPart.Bytes`, sent verbatim) and `Reader` (`OpenContent` `Func<Stream>`, `ContentLength`,
  `null` unknown -> chunked); both take a text part's headers, and a reader is never read ahead.
- Choice: `EasyMimeBytes` that are valid UTF-8 stay text and only other bytes become a `Bytes` part. The
  tool's `-F` text reaches the same mapping as UTF-8 `EasyMimeBytes` and must keep its platform (ANSI on
  Windows) encoding; the engine's builder is UTF-8, so valid UTF-8 re-encodes to the same bytes and every
  `curl_mime_data` part still goes out byte for byte. This kept `Curl.Console` out of the task.
- `EasyMimeCallback` maps to a `Reader` over the new `MimeCallbackReadStream` (a `CallbackReadStream`);
  the perform collects them while building and any `CURL_READFUNC_ABORT` gives `AbortedByCallback` with
  `operation aborted by callback`. With a seek callback the stream seeks, so the body rewinds on a resend
  through `(0, SEEK_SET)`; a failed seek throws `IOException("seek callback returned error <n>")`, which the
  HTTP handler does not yet turn into exit 65 (noted in the ADR's consequences).
- Measured: an unknown-size body is sent `Transfer-Encoding: chunked` with `Expect: 100-continue`, as libcurl.
  The known-size callback request equals the tool's request for `-F c=hello` byte for byte.
- Quality: Core and Engine 100% line and branch, 0 failing members (first run found `AddPartAsync`,
  `KindOf`, `FromMimePart` over complexity and a `DefaultContentType` branch; split and re-measured).

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. CURLOPT_MIMEPOST sends curl_mime_data_cb parts (known size with Content-Length, -1 chunked, abort and seek honoured) and non-UTF-8 curl_mime_data bytes verbatim
