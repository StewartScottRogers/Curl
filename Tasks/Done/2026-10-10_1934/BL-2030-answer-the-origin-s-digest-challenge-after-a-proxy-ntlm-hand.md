---
id: BL-2030
title: Answer the origin's Digest challenge after a proxy NTLM handshake as curl does (upstream test169)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-2030 — Answer the origin's Digest challenge after a proxy NTLM handshake as curl does (upstream test169)

## Goal

Upstream test169 (`--proxy-ntlm` to a forward proxy, then `--digest` to the origin) passes through
the conformance harness: after the proxy's NTLM handshake completes, the origin's Digest challenge
is answered with `Authorization: Digest ...` as curl 8.21.0 does.

## Context

Found by BL-1999 (GF-0003 re-close). With hand-built NTLM the conformance harness reports
test169: `<verify><protocol> differs at byte 782 (line 17): expected "Authorization: Digest
username=\"digest\", realm=\"r e a l m\", nonce=\"abcdef\", uri=\"/169\",
response=\"89b737a4b6eefde285c093c92e9bd6ea\"\r\n", got "Proxy-Authorization: NTLM TlRMTVNTUAADAAAA..."`:
Curl re-sends the proxy's type-3 message where curl, its proxy authentication done, sends the
origin's Digest answer. Read the case in `Curl.Conformance.UnitTests/UpstreamTestData/test169` first.

## Acceptance criteria

- [x] test169 is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` and the conformance ratchet passes.
- [x] A unit test pins the request sequence: proxy type-1, proxy type-3, then origin Digest without a repeated `Proxy-Authorization: NTLM`.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Cause: the 401 retry kept the proxy's `Proxy-Authorization` value via `RepeatProxyAuthorization`, which re-sent NTLM Type 3. curl treats a sent Type 3 as a finished, connection-bound handshake and sends only the origin's answer after it. `HttpProtocolHandler.RepeatProxyAuthorization` now keeps nothing after a Type 3 (detected by its base64 prefix `NTLM TlRMTVNTUAADAAAA`, as BL-2029 detects Type 1). Scope kept to NTLM Type 3: Basic and Digest are still kept (BL-869 pins Digest's kept answer). No ADR: the expected bytes are upstream test169's, so this matches curl 8.21.0 directly.
- Unit test: `HttpProtocolHandlerTests.ExecuteAsync_ProxyNtlmThenOriginDigest_SendsTheDigestAnswerWithoutTheProxysType3`; its Digest response hash checked by computing test169's own expected hash the same way (89b737a4...).
- Curl.Authentication.UnitLibrary needed no change.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. test169 passes and is listed; proxy NTLM Type 3 no longer re-sent on the origin's Digest retry
