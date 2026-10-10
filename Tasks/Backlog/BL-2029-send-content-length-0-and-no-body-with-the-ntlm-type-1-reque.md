---
id: BL-2029
title: Send Content-Length: 0 and no body with the NTLM type-1 request of a POST or PUT (test170, 176, 239, 243, 267)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-2029 — Send Content-Length: 0 and no body with the NTLM type-1 request of a POST or PUT (test170, 176, 239, 243, 267)

## Goal

A POST or PUT that starts an NTLM handshake (`--ntlm`, `--proxy-ntlm`, `--anyauth` picking NTLM)
sends its type-1 request with `Content-Length: 0` and no body, as curl 8.21.0 does, so upstream
test170, 176, 239, 243 and 267 pass through the conformance harness.

## Context

Found by BL-1999 (GF-0003 re-close). With hand-built NTLM, the conformance harness
(`UpstreamConformanceTests.UpstreamCase_RunThroughCurl_HoldsTheRatchet`) reports:
test170 `expected "Content-Length: 0\r\n", got "Content-Length: 157\r\n"` (line 7), test176 got 11
(line 6), test239 got 6 (line 7), test243 got 6 (line 15), test267 got 4 (line 8). curl's
`http.c` (`Curl_http_bodysend` / `http_perhapsrewind`, `authneg`) sends an empty body while the
NTLM or Negotiate negotiation is under way and the real body only with the final credential.

## Acceptance criteria

- [ ] test170, 176, 239, 243 and 267 are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` and the conformance ratchet passes.
- [ ] A unit test in `Curl.Protocol.Http.UnitTests` pins the type-1 request's `Content-Length: 0` and empty body for a `-d` POST under `--ntlm`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-10: Created.
