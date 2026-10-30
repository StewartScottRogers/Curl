---
id: BL-4307
title: ResumableUpload extension: IETF and tus upload flows with offset retrieval and resumption in a transfer hook
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4306]
touches: [Curl.Extension.ResumableUpload.UnitLibrary, Curl.Extension.ResumableUpload.UnitTests]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4307 — ResumableUpload extension: IETF and tus upload flows with offset retrieval and resumption in a transfer hook

## Goal

`Curl.Extension.ResumableUpload.UnitLibrary` has the transfer hook that uploads `-T`'s body resumably: by tus 1.0 when the server offers it, otherwise by the IETF resumable upload draft, resuming from the server's offset after a broken connection.

## Context

Split from BL-4129 for size; builds on BL-4306's HTTP/1.1 exchange and header fields. BL-4129 then adds the option, the catalogue lines, the description and the ADR, and gives `HookFor(ExtensionOptionValues)` its option, so expose the hook's constructor (and anything `HookFor` needs) for it.

Suggested flow (record any change under Notes for BL-4129's ADR): the hook implements `ITransferHook.ExecuteAsync(context, services, next)`; for a transfer that is not http/https or has no `context.Upload`, it calls `next` unchanged. Otherwise it sends `OPTIONS` to the URL through `services.Connector`: a response with `Tus-Resumable: 1.0.0` and `Tus-Version` naming 1.0.0 and `Tus-Extension` naming `creation` selects tus (tus 1.0 "Creation": `POST` with `Upload-Length`, 201 with `Location`; "Core Protocol": `PATCH` with `Content-Type: application/offset+octet-stream` and `Upload-Offset`, `HEAD` for the offset). Otherwise the IETF draft: upload creation (`POST`/`PUT` per curl's method, `Upload-Complete: ?0`, `Upload-Length`, `Upload-Draft-Interop-Version`), the 201's `Location` as the upload resource, upload append (`PATCH`, `Content-Type: application/partial-upload`, `Upload-Offset`, `Upload-Complete: ?1`), and after a broken connection offset retrieval (`HEAD`, `Upload-Offset`) and append from there. Resume at most a fixed number of times. The final response's body, `-i` headers and status are the transfer's (`context.Output`, `context.HeaderOutput`, `TransferResult`); a failure ends with curl's exit code for it (7 connect, 55 send, 56 receive, 22 only with `-f`). Non-seekable upload streams: buffer what was sent past the server's last acknowledged offset or refuse resumption, and say which under Notes.

## Acceptance criteria

- [x] Without an upload, or for a scheme other than http/https, the hook calls `next` unchanged; unit test.
- [x] A server answering `OPTIONS` with tus 1.0 headers gets tus creation and `PATCH` requests; one interrupted mid-body gets a `HEAD` and a `PATCH` from its `Upload-Offset`; unit tests over fake connections naming tus 1.0 sections.
- [x] Any other server gets the IETF draft's upload creation, append, and after an interruption offset retrieval and append from the returned offset; unit tests over fake connections naming the draft's sections.
- [x] The final response's status, headers and body reach the transfer's outputs and result, and a failure ends with the matching curl exit code; unit tests.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Plan and decisions in ADR-1097 (for BL-4129's option ADR). Code in `Curl.Extension.ResumableUpload.UnitLibrary/Transfer/`; the library now references `Curl.Extension.Abstractions` (its csproj is inside this task's touches).
- For BL-4129: `HookFor` constructs `new ResumableUploadTransferHook(maxResumptions)` (public; default `DefaultMaxResumptions` = 3, chosen as a small fixed bound).
- Changes to the suggested flow: tus is chosen by a 2xx `OPTIONS` whose `Tus-Version` names 1.0.0 and whose `Tus-Extension` names `creation`; `Tus-Resumable` is not required there, because tus 1.0 exempts OPTIONS from it. The IETF upload resource is the creation's 2xx `Location` only (a 104's is not read). IETF interop version 6, matching the field type's documentation (draft -05).
- Non-seekable upload streams (and seekable ones already read part way) are buffered whole in memory, because both protocols announce `Upload-Length` at creation and a resumption re-sends from any offset.
- Exit codes: the connector's own (7), 55 send, 56 receive (including a connection closed before the response), 8 for a malformed response or an offset outside the upload, 22 with `-f`/`--fail-with-body` for a status of 400 or more.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Extension.ResumableUpload.UnitLibrary` gives 100% line, 100% branch, max complexity 10, no failing member. Full `dotnet build` is clean and the fast tests are green (143 in this project). The tests use no paths, OS error text or certificates, so they are platform-neutral.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 557ffc32 c1a1811d af3a68c0 edfd5ba3 3180db99 3d836d5e.
- 2026-10-29: Doing -> Done. ResumableUploadTransferHook uploads -T bodies by tus 1.0 or the IETF draft and resumes from the server's offset
