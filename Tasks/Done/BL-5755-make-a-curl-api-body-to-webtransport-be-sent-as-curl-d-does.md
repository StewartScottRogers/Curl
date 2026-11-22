---
id: BL-5755
title: Make a Curl.Api body to webtransport:// be sent, as curl -d does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-5753]
touches: [Curl.Extended.Protocol.WebTransport.UnitLibrary, Curl.Api/RequestBodies/UploadSchemes.cs, Curl.Api.UnitTests/RequestBodies/UploadSchemeBodyTests.cs]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-5755 — Make a Curl.Api body to webtransport:// be sent, as curl -d does

## Goal

A `Curl.Api` `POST` with a body to a `webtransport://` URL sends the body on the session and returns what the server sends back, as `curl -d @file webtransport://...` does.

## Context

From Surly cookbook bug report `2026-11-22T1116-Curl-bug-report.md` (batch 11 of the 1,000-recipe plan), finding 3. Build tested: Curl `c84035f797`, Windows 11; server Surl `5da5b3eaf`. Check it still reproduces on the current branch first.

`surl --cert server.pem --key server.key --user a:b webtransport://127.0.0.1:4433/`; a `Curl.Api` `POST` with a 100-byte body gets status 200 with an empty body, and surl records no data. `curl -d @file webtransport://...` echoes it. Same family as BL-5611, BL-5681 and the MCP task filed with this one: the WebTransport extension reads `-d` data that a `Curl.Api` body does not supply in the same form. Cover the stream, `?wt=uni` and `?wt=datagram` modes. Cookbook recipe: `webtransport-send-datagrams-and-streams-in-csharp` (retitled "Opening a WebTransport Session in C#" until this lands).

## Acceptance criteria

- [x] Against Surl's WebTransport echo (or the test double), a `Curl.Api` `POST` with a body gets it echoed back on a stream, with `?wt=uni`, and with `?wt=datagram`; the Notes record the runs.
- [x] A unit test pins that a `Curl.Api` body reaches the WebTransport client as `-d` data.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Cause found before any edit: WebTransportProtocolHandler already reads context.PostData and context.Upload (as -d and -T), but Curl.Api/RequestBodies/UploadSchemes.cs does not list "webtransport", so a Curl.Api body becomes HTTP post fields. Fix: add "webtransport" to UploadSchemes and a DataRow in UploadSchemeBodyTests (as BL-5681 did for amqp etc.), plus a WebTransport handler test that an Upload stream body is sent. Curl.Api/RequestBodies is held by BL-5753 in Doing, so this waits on it (depends-on). Nothing edited yet.

Second claim: already fixed on this branch by BL-5758 (c267034e5f, ADR-1336: a Curl.Api body to any extension scheme is -d data) and BL-5754 (stream bodies read up front). So webtransport is NOT added to UploadSchemes: curl sends it with -d, not -T, and the handler reads context.PostData first. Decided by Claude under Stewart's delegation; no new ADR, ADR-1336 already records it.

Run against Surl (Z:/repos/Surl Surl.Console Debug surl.exe --cert server.pem --key server.key --user a:b webtransport://127.0.0.1:44355/) with a file-based app referencing this branch's Curl.Api, POST of a 100-byte body, CA pinned with CertificateAuthorityBlob: stream status 0 bytes 100 echo=True; ?wt=uni status 0 bytes 100 echo=True; ?wt=datagram status 0 bytes 100 echo=True; surl logged three webtransport exchanges.

Added UploadSchemeBodyTests.SendAsync_BytesBodyPostedToWebTransport_ReachesTheClientAsDashDData (stream, uni, datagram rows): Upload false, Http.Post true, PostFields the body. The handler's -d send in each mode is already pinned in WebTransportProtocolHandlerTests. Test-only change, so no Measure-CodeQuality run. dotnet build clean (0 warnings); fast tests: 115 test projects passed, 0 failed. CI on Linux and macOS: the test is platform-neutral (no path, OS error or certificate).

## Log

- 2026-11-22: Created from Surly bug report 2026-11-22T1116, finding 3.
- 2026-11-22: Backlog -> Doing. Criteria at claim: 8ab14af5 80c1f77b 3d836d5e.
- 2026-11-22: Doing -> Backlog. Needs Curl.Api/RequestBodies/UploadSchemes.cs (add webtransport scheme), held by BL-5753 in Doing; waits on it
- 2026-11-22: Backlog -> Doing. Criteria at claim: 8ab14af5 80c1f77b 3d836d5e.
- 2026-11-22: Doing -> Done. A Curl.Api POST body to webtransport:// is sent as -d data and echoed back in stream, uni and datagram modes
