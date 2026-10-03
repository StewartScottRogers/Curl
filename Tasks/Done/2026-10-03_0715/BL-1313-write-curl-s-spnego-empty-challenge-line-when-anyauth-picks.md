---
id: BL-1313
title: Write curl's SPNEGO empty challenge line when --anyauth picks Negotiate on a challenge that starts with =
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1313 — Write curl's SPNEGO empty challenge line when --anyauth picks Negotiate on a challenge that starts with =

## Goal

When Negotiate is picked only after the challenge (`--anyauth`, or `--negotiate --basic`) with `-u`, and the 401's `Negotiate` token starts with `=` (`WWW-Authenticate: Negotiate =`), Curl writes curl 8.21.0's `SPNEGO handshake failure (empty challenge message)` info line, steps no security context and sends no further request, as curl does.

## Context

- BL-1303 put the `=` test (`NegotiateHttpAuthenticator.EmptyChallengeMessageLine`) on `StepWithoutAnsweringAsync` and `ContinueAuthorizationAsync`. The third path, `RankedHttpAuthenticator.CreateAuthorizationAsync` when `AnswersWithNegotiate` is true with challenges, calls `NegotiateHttpAuthenticator.CreateAuthorizationAsync(request, ct)`, which never sees the challenge, so it steps a context and (ADR-0232) answers `string.Empty` to ask for the request again.
- curl 8.21.0: `Curl_input_negotiate` calls `Curl_auth_decode_spnego_message` (`lib/vauth/spnego_sspi.c` 173-185, `spnego_gssapi.c` 132-143), which returns `CURLE_BAD_CONTENT_ENCODING` after the line; the caller sets `authproblem`, so no further request. Measure with `Record-CurlExchange.ps1 -Response 'HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Negotiate =\r\nContent-Length: 0\r\n\r\n'` and `-sv --anyauth -u : URL` before pinning output.

## Acceptance criteria

- [x] Real curl 8.21.0 measured for `--anyauth -u :` against `Negotiate =`; the stderr and exit code recorded under Notes.
- [x] A test in `Curl.Authentication.UnitTests` drives `RankedHttpAuthenticator.CreateAuthorizationAsync` with `--anyauth` and `["Negotiate ="]` and asserts what curl was measured to do: no empty-challenge line, one context stepped, and `string.Empty` returned (a further request without a header).
- [x] `dotnet build Curl.Authentication.UnitTests -warnaserror` is clean; `dotnet test Curl.Authentication.UnitTests --filter "TestCategory!=Integration"` passes.

## Notes

- Measured 2026-10-03, curl 8.21.0 (x86_64-w64-mingw32, Schannel), `Record-CurlExchange.ps1 -Response <401 Negotiate => x3 -CurlArgs -sv --max-time 8 --anyauth -u : URL` (and the same with `--negotiate --basic`). stderr after the 401:
  `* Issue another request to this URL: 'http://127.0.0.1:47314/'`, `* Reusing existing http: connection with host 127.0.0.1`, `* InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package`, `* Server auth using Negotiate with user ''`, then a second `GET /` with no `Authorization` header. **No `SPNEGO handshake failure (empty challenge message)` line.** Exit code: the recorder serves one response per connection, so curl's retry on a fresh connection timed out (28, from `--max-time`); without `--max-time` it hung for 30 minutes. The exit code that matters here is "a further request is sent", which it is.
- Why: `Curl_auth_decode_spnego_message` decodes the challenge (and writes the line for a token starting with `=`) only when a context already exists - the second leg BL-1303 pinned. On the first leg (`--anyauth`, Negotiate picked from the 401) there is no context, so the token is ignored and a fresh context steps.
- Decision (measured, no ADR needed - it is ADR-0232's existing behaviour): the task's premise was wrong. Curl already matches: `RankedHttpAuthenticator.CreateAuthorizationAsync` steps one context, reports its failure only, and answers `string.Empty` (ask again without a header). No production change; pinned with `NegotiateEmptyChallengeMessageTests.CreateAuthorizationAsync_NegotiatePickedAfterA401WithEquals_StepsAContextWithoutTheLineAndAsksAgain` (`--anyauth` SSPI and GSS-API wording, `--negotiate --basic`). Acceptance criterion 2 reworded from "asserts the info line ... no context created" to what curl was measured to do.
- Build `dotnet build Curl.Authentication.UnitTests -warnaserror`: 0 errors. Tests: 800 passed, 4 skipped (platform-conditional), 0 failed. `dotnet format --verify-no-changes` clean.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. Measured: curl 8.21.0 writes no empty-challenge line when --anyauth picks Negotiate from a 401 with 'Negotiate ='; Curl already matches (steps a context, asks again), now pinned by a test
