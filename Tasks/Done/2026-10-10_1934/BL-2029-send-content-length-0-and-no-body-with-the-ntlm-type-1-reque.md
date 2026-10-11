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
completed: 2026-10-10
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

- [x] test170, 176, 239, 243 and 267 are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` and the conformance ratchet passes.
- [x] A unit test in `Curl.Protocol.Http.UnitTests` pins the type-1 request's `Content-Length: 0` and empty body for a `-d` POST under `--ntlm`.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- No new ADR: every byte here is pinned by upstream's tests of curl 8.21.0 (test170, 176, 239, 243, 267), so nothing was left to decide. The NTLM Type 1 request reuses the Digest probe (`HttpRequestFraming.AsAuthProbe`, ADR-0441): the first request probes when its `Authorization` or `Proxy-Authorization` is a Type 1 value (`NTLM TlRMTVNTUAABAAAA` prefix, `HttpProtocolHandler.SendsNtlmProbe`), and so does a retry answering a challenge with one (`--anyauth`, test243, `HttpRequestPlan.WithAnswer`).
- A `-F` form's probe leaves out its `Content-Type` (test170; `HttpRequestHeadFormatter.BodyContentTypeOf`). This applies to the Digest probe too, as curl's `authneg` skips preparing the MIME headers for both.
- A Type 1 probe answered with a 2xx is resent with the body and without the Type 1 value (test176; `HttpRequestPlan.WithProbedBody`).
- Negotiate's first token gets no probe here: curl's `authneg` covers it as well, but no listed case needs it, and a Negotiate token has no fixed prefix to recognise. Left for a case that pins it.
- Measure-CodeQuality.ps1 not run (budget); every new branch has a unit test: origin `-d` and `-F` probes, the 2xx resend, the `--anyauth` retry probe, and the `--proxy-ntlm` probe and resend.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. NTLM Type 1 POST/PUT sends Content-Length: 0 and no body; test170, 176, 239, 243, 267 pass
