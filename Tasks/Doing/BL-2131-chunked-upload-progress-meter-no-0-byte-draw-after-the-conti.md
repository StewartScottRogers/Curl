---
id: BL-2131
title: Chunked-upload progress meter: no 0-byte draw after the continue wait, count chunk framing, keep the transfer start across auth retries
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-13
completed:
---
# BL-2131 — Chunked-upload progress meter: no 0-byte draw after the continue wait, count chunk framing, keep the transfer start across auth retries

## Goal

A chunked upload's progress meter matches curl 8.21.0: no 0-byte line after the 100-continue wait, uploaded bytes counting chunk framing, and an auth retry keeping the transfer's start time if curl does.

## Context

Found while diagnosing GF-0007 on 2026-10-13 (upstream test1072; byte differences the gap office's meter stripping hides):

- `Curl.Protocol.Http.UnitLibrary/HttpRequestBodyWriter.cs` around line 151 reports `ReportUploaded(0)` before the body; after the 1-second continue wait that draws a 0-byte line at 00:01. Real curl draws only after the first piece goes out (`100 128 ... 00:01`).
- Curl's upload count leaves out chunk framing (`HttpRequestBodyWriter.cs` around lines 459-460, `HttpTransferProgress.cs` around line 70); curl counts it (128, then 133 for a 122-byte body).
- Probable, needs measuring: `Curl.Console/TransferProgressRecorder.cs` around line 274, `StartHop` resets the start time on each auth-retry request; curl's figures (test1072's retry line shows Spent 00:01; test1075's speeds fall steadily) suggest it keeps the transfer's start for auth retries but not redirect hops.

Measure real curl with `Record-CurlExchange.ps1` before pinning any figures.

## Acceptance criteria

- [ ] No progress line is drawn for the report made before the body when it comes after the continue wait.
- [ ] For a chunked body the uploaded count includes chunk framing, as curl's does (pinned from a measurement).
- [ ] The auth-retry start-time behaviour is measured against curl and matched; the Notes record the measurement.
- [ ] Unit tests pin each of the three.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.

## Notes

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
