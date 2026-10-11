---
id: BL-2059
title: Restart --anyauth probing on a -L redirect, so the first request to the new URL carries no Authorization (upstream test90)
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-11
completed: 2026-10-11
---
# BL-2059 — Restart --anyauth probing on a -L redirect, so the first request to the new URL carries no Authorization (upstream test90)

## Goal

With `--anyauth -L`, Curl's first request to a redirect target carries no `Authorization:` header, as curl 8.21.0's does, so the server's 401 picks the scheme again.

## Context

Found by BL-2019 (GF-0004). Upstream test90 (`http://%HOSTIP:%HTTPPORT/90 -u testuser:testpass --anyauth -L`) expects six requests: `GET /90` with no auth, the NTLM type 1, the NTLM type 3 (answered with a redirect to `/you/900010`), then `GET /you/900010` with no auth, the type 1, the type 3. Under the gap harness at 2fd5b394d Curl's fourth request already carries `Authorization: NTLM TlRMTVNTUAABAAAABoIIAAAAAAAAAAAAAAAAAAAAAAA=` (first difference at byte 552, line 20: expected `User-Agent: curl/8.21.0`). So Curl keeps the scheme `--anyauth` picked for the first URL; curl starts probing again for the new one.

Start in `Curl.Protocol.Http.UnitLibrary` where a followed redirect builds its next request's authentication (`HttpAuthUsingLines.cs` holds the `--anyauth` wiring). Measure real curl with `Record-CurlExchange.ps1` before pinning: an `--anyauth -L` exchange whose 401 offers NTLM (or Basic, which needs no Windows SSPI) and whose authenticated response is a 301 to a second path on the same host.

## Acceptance criteria

- [x] A unit test in `Curl.Protocol.Http.UnitTests` pins that with `--anyauth` and `-L` the first request to the redirect target carries no `Authorization:` header, and that the second carries the scheme its 401 picked.
- [x] The test's expected requests come from a real curl 8.21.0 exchange recorded with `Record-CurlExchange.ps1`, named in Notes.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Measured with Record-CurlExchange.ps1 on curl 8.21.0 (Schannel): `--anyauth -u testuser:testpass -L` against NTLM 401s answered by a 301 to /two. `GET /two` first request carries no Authorization; `GET /one` likewise. Basic is still kept across hops (test1088, BL-1819).
- Decision: a picked NTLM or Negotiate (connection schemes) is not kept for the next hop in `AllowedSchemesOf`; Basic, Digest, Bearer still are. Unit test: `ExecuteAsync_EarlierHopPickedAConnectionScheme_ProbesAgainWithEverySchemeAllowed`.

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
- 2026-10-11: Doing -> Done. NTLM/Negotiate no longer kept across -L hops
