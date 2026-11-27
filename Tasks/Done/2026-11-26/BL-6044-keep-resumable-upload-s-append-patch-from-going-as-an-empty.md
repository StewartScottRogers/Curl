---
id: BL-6044
title: Keep --resumable-upload's append PATCH from going as an empty --digest or --ntlm probe that completes an IETF draft upload early
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.ResumableUpload.UnitLibrary/Transfer, Curl.Extension.ResumableUpload.UnitTests/Transfer, Curl.Extension.ResumableUpload.UnitLibrary/CLAUDE.md, Curl.Protocol.Abstractions.UnitLibrary/HttpRequestOptions.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpRequestHeadFormatter.cs, Curl.Original.Protocol.Http.UnitTests/HttpProtocolHandlerTests.AuthProbeHeaders.cs]
requirement: none
created: 2026-11-26
completed: 2026-11-26
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

- [x] With `--digest` and with `--ntlm`, a server that asks for no authentication receives no IETF draft append with
      `Upload-Complete: ?1` and empty content for a non-empty upload.
- [x] A server that does require Digest or NTLM on the append still receives the whole upload.
- [x] A focused unit test in `Curl.Extension.ResumableUpload.UnitTests` pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Decision (ADR-1440): new `HttpRequestOptions.AuthProbeHeaders` gives the empty probe its own header fields; the
  IETF append's probe says `Upload-Complete: ?0`. No command-line option sets it, so curl's transfers probe as before.
  No option changed, so `--ai-help` is unaffected.
- A first design (content as `?0`, then an empty `?1` request) was built and dropped: it still sent an empty `?1`
  append, which the first criterion rules out.
- `touches` widened to the three files outside the extension's `Transfer` folders the property needs, and the
  extension's CLAUDE.md. The only other task in Doing near them, BL-6036, names `Http2Session.cs` and its test.
- Measured with the built `curl.exe` against a loopback server (HTTP/1.1): with no authentication, `--digest` and
  `--ntlm` send `PATCH` len 0 `?0`, then len 5 `?1`; Digest and NTLM servers (NTLM with a 2,000,000-byte upload)
  receive the whole upload in the authenticated `?1` request.
- Fast tests: one full run, whose only failure was a row of the new HTTP test (wrong fake authenticator), fixed in
  that test and its project rerun green. No production code changed after the full run.
- Quality: `Curl.Extension.ResumableUpload.UnitLibrary` 100% line and branch, 0 failing. The three-library run that
  runs every referencing test project was too slow for the deadline (45 minutes, stopped), so the Http and
  Abstractions libraries were scored from their own test projects' coverage only: worst CRAP 10, no member over the
  complexity gate. 12 members there were short of coverage in that narrow measurement; none is one this change
  touches.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 5a00fb8f 44796430 97f2967b 3d836d5e.
- 2026-11-26: Doing -> Done. The IETF append's empty --digest or --ntlm probe now says Upload-Complete: ?0 (ADR-1440), so it completes nothing and the whole upload follows as ?1.
