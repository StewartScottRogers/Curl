---
id: BL-4300
title: ResumableUpload extension: IETF and tus upload flows with offset retrieval and resumption in a transfer hook
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4299]
touches: [Curl.Extension.ResumableUpload.UnitLibrary, Curl.Extension.ResumableUpload.UnitTests]
requirement: none
created: 2026-10-29
completed:
---
# BL-4300 — ResumableUpload extension: IETF and tus upload flows with offset retrieval and resumption in a transfer hook

## Goal

`Curl.Extension.ResumableUpload.UnitLibrary` has the transfer hook that uploads `-T`'s body resumably: by tus 1.0 when the server offers it, otherwise by the IETF resumable upload draft, resuming from the server's offset after a broken connection.

## Context

Split from BL-4129 for size; builds on BL-4299's HTTP/1.1 exchange and header fields. BL-4129 then adds the option, the catalogue lines, the description and the ADR, and gives `HookFor(ExtensionOptionValues)` its option, so expose the hook's constructor (and anything `HookFor` needs) for it.

Suggested flow (record any change under Notes for BL-4129's ADR): the hook implements `ITransferHook.ExecuteAsync(context, services, next)`; for a transfer that is not http/https or has no `context.Upload`, it calls `next` unchanged. Otherwise it sends `OPTIONS` to the URL through `services.Connector`: a response with `Tus-Resumable: 1.0.0` and `Tus-Version` naming 1.0.0 and `Tus-Extension` naming `creation` selects tus (tus 1.0 "Creation": `POST` with `Upload-Length`, 201 with `Location`; "Core Protocol": `PATCH` with `Content-Type: application/offset+octet-stream` and `Upload-Offset`, `HEAD` for the offset). Otherwise the IETF draft: upload creation (`POST`/`PUT` per curl's method, `Upload-Complete: ?0`, `Upload-Length`, `Upload-Draft-Interop-Version`), the 201's `Location` as the upload resource, upload append (`PATCH`, `Content-Type: application/partial-upload`, `Upload-Offset`, `Upload-Complete: ?1`), and after a broken connection offset retrieval (`HEAD`, `Upload-Offset`) and append from there. Resume at most a fixed number of times. The final response's body, `-i` headers and status are the transfer's (`context.Output`, `context.HeaderOutput`, `TransferResult`); a failure ends with curl's exit code for it (7 connect, 55 send, 56 receive, 22 only with `-f`). Non-seekable upload streams: buffer what was sent past the server's last acknowledged offset or refuse resumption, and say which under Notes.

## Acceptance criteria

- [ ] Without an upload, or for a scheme other than http/https, the hook calls `next` unchanged; unit test.
- [ ] A server answering `OPTIONS` with tus 1.0 headers gets tus creation and `PATCH` requests; one interrupted mid-body gets a `HEAD` and a `PATCH` from its `Upload-Offset`; unit tests over fake connections naming tus 1.0 sections.
- [ ] Any other server gets the IETF draft's upload creation, append, and after an interruption offset retrieval and append from the returned offset; unit tests over fake connections naming the draft's sections.
- [ ] The final response's status, headers and body reach the transfer's outputs and result, and a failure ends with the matching curl exit code; unit tests.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
