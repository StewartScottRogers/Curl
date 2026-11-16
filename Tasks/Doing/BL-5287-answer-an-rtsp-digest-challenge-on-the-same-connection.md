---
id: BL-5287
title: Answer an RTSP Digest challenge on the same connection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Rtsp.UnitLibrary/RtspProtocolHandler.cs, Curl.Original.Protocol.Rtsp.UnitTests/RtspDigestChallengeTests.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5287 — Answer an RTSP Digest challenge on the same connection

## Goal

With `--digest` or `--anyauth` on an `rtsp://` URL, the port answers a `401` carrying a `WWW-Authenticate: Digest` challenge by re-sending the request with `Authorization: Digest ...` on the same connection, as upstream curl 8.21.0 does.

## Context

Filed from Surl's Phase 7 measurement of the port (Stewart approved filing on the port's board, 2026-11-16): Surl `Documentation/Conformance/CurlPort/Port-Board-Handoff.md`, entry 10 (github.com/StewartScottRogers/Surl). The port was measured at commit `b42c319ba940f66452b2b6f3e9910d46825b08fe` (port `curl.exe` SHA-256 `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7`), on Windows x64, 2026-11-05, beside upstream curl 8.21.0, the oracle: where the two differ against the same Surl server, the port is the one in the wrong.

- **Finding:** Surl `Documentation/Conformance/CurlPort/Rtsp.md`, RTSP-1.
- **Surl tests (3):** `CurlPortBesideUpstreamCurlOverRtspTests.Options_Digest_Exits0OnOneConnection`, `Options_DigestWrongPassword_Exits22With401`, `Options_AnyAuth_Exits0WithDigest`; Surl commit `fdb0ab43767137d4ce308750d887b2fb2b2ab625`.
- **Command lines** (against `surl rtsp://... --user tester:secret`):
  - `curl -sS -m 20 -f --digest -u tester:secret -w %{num_connects} rtsp://127.0.0.1:<port>/clip.bin`
  - `curl -sS -m 20 -f --digest -u tester:wrong -w %{response_code} rtsp://127.0.0.1:<port>/clip.bin`
  - `curl -sS -m 20 -v -f --anyauth -u tester:secret rtsp://127.0.0.1:<port>/clip.bin`
- **Upstream curl 8.21.0 (correct):** reference build, SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`: `OPTIONS *` without credentials, `401`, a second `OPTIONS *` with `Authorization: Digest ...` on the same connection; exit 0 (`-w %{num_connects}` writes `1`), or 22 with `401` for the wrong password.
- **Port (defect):** sends the first `OPTIONS *` only and stops; first differing request byte: connection 0, offset 57 (upstream `0x4F`, the second request's `O`; the port's bytes end). Exit 22 where upstream exits 0.
- **What decides:** RFC 2326 section 14 takes RTSP's authentication from HTTP; RFC 7616 section 3.4; curl 8.21.0 `lib/rtsp.c` builds every request through `Curl_http_output_auth` and follows a 401.
- **Where to start:** `Curl.Original.Protocol.Rtsp.UnitLibrary/RtspProtocolHandler.cs`.

## Acceptance criteria

- [ ] `--digest -u tester:secret` on `rtsp://` against a server that challenges with Digest exits 0 on one connection (`-w %{num_connects}` writes `1`), shown by a port test.
- [ ] `--anyauth` picks Digest from the challenge and succeeds, and a wrong password exits 22 after the second request, shown by port tests.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: a2ae5627 2476711f.
