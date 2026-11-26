---
id: BL-6044
title: Keep --resumable-upload's append PATCH from going as an empty --digest or --ntlm probe that completes an IETF draft upload early
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.ResumableUpload.UnitLibrary/Transfer, Curl.Extension.ResumableUpload.UnitTests/Transfer]
requirement: none
created: 2026-11-26
completed:
---
# BL-6044 — Keep --resumable-upload's append PATCH from going as an empty --digest or --ntlm probe that completes an IETF draft upload early

## Goal

`--resumable-upload` with `--digest` or `--ntlm` completes an IETF draft upload against a server that answers the append
without asking for authentication.

## Context

Found in BL-6034 (EXT-129, ADR-1428), which fixed the creation request only. The append `PATCH` in
`Transfer/ResumableUploadFlow.cs` has a body, so curl's HTTP handler sends it as an empty probe (`Content-Length: 0`,
`HttpProtocolHandler.FirstAuthorization.cs`) and, after a 2xx, again with the body; measured with a loopback tus mock:
`OPTIONS(0) POST(0) PATCH(0) PATCH(3)`. tus appends nothing for the empty one. The IETF draft's probe carries
`Upload-Complete: ?1`, so a server that does not challenge it may complete the upload with no bytes or reject it (any
non-2xx ends the transfer without the body). `--extended-help` and ADR-1103 are the specification.

## Acceptance criteria

- [ ] With `--digest` and with `--ntlm`, a server that asks for no authentication receives no IETF draft append with
      `Upload-Complete: ?1` and empty content for a non-empty upload.
- [ ] A server that does require Digest or NTLM on the append still receives the whole upload.
- [ ] A focused unit test in `Curl.Extension.ResumableUpload.UnitTests` pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
