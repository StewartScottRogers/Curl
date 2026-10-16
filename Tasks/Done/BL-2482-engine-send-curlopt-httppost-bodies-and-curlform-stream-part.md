---
id: BL-2482
title: Engine: send CURLOPT_HTTPPOST bodies and CURLFORM_STREAM parts through the read callback
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2470]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2482 — Engine: send CURLOPT_HTTPPOST bodies and CURLFORM_STREAM parts through the read callback

## Goal

A perform from the options with `EasyHttpOptions.HttpPost` (`CURLOPT_HTTPPOST`) posts that form as curl 8.22.0 does, a `CURLFORM_STREAM` part's content read through the handle's `CURLOPT_READFUNCTION` with the stream object as its argument.

## Context

BL-2440 (ADR-0618) built `ManagedFormBuilder`: `Build` gives the `EasyMimeBody` `Curl_getformdata` makes, and `Libcurl`'s `CURLOPT_HTTPPOST` setter stores it on `EasyHttpOptions.HttpPost`. Nothing in `Curl.Engine.UnitLibrary` reads `HttpPost` yet (`ManagedEasyTransfer` builds only `MimePost` through `MimePostForm`), and a `CURLFORM_STREAM` part's `Data` is `null`, because the read callback is the easy handle's (upstream passes `data->state.fread_func` to `Curl_getformdata`). Sending a callback part needs BL-2470 first. Start at `MimePostForm.BuildAsync` and `FormPost.DataOf`.

## Acceptance criteria

- [x] A perform with `HttpPost` set and no `MimePost` sends the form body, compared with the tool's `-F` request for the same fields, in `ManagedEasyTransferTests`.
- [x] A `CURLFORM_STREAM` field's bytes come from `EasyCallbackOptions.Read`, `CURLFORM_CONTENTSLENGTH` as its size (0 is unknown, sent chunked), pinned by a test.
- [x] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members.
- [x] `Curl.Engine.UnitLibrary/CLAUDE.md` is kept true.

## Notes

- Plan (ADR-0650, decided by Claude under Stewart's delegation): `FormPost` gives a `CURLFORM_STREAM` part an
  engine-internal `FormStreamData` (contents length, stream object); `MimePostForm.BodyOf` sends `MimePost`, else
  `HttpPost` (not to a mail URL, as upstream reads it only in `http.c`) with each `FormStreamData` turned into an
  `EasyMimeCallback` over `EasyCallbackOptions.Read`, 0 length as -1 (chunked). The rest is the MIMEPOST path.
- Choice: the stream object is not passed to the read callback - a managed `ReadCallback` has no user pointer
  (its closure is its data, ADR-0573). With no read callback the part is sent empty; `curl_formget` still sends
  a stream part empty. `Curl.Api` was not changed, keeping the task inside its touches.
- Measured: the `HttpPost` request for `a=1` and a 5-byte stream `c` equals the tool's `-F a=1 -F c=hello`
  request byte for byte; a stream with no `CURLFORM_CONTENTSLENGTH` goes chunked. Build clean, fast tests green,
  `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` 0 failing members.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 0e6d98c3 2be10853 a618ea01 aa1ad728.
- 2026-10-16: Doing -> Done. CURLOPT_HTTPPOST forms are sent by a perform, byte-identical to the tool's -F request, CURLFORM_STREAM parts read through CURLOPT_READFUNCTION (0 length chunked)
