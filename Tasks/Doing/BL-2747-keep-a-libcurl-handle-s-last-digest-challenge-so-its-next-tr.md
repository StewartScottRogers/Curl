---
id: BL-2747
title: Keep a Libcurl handle's last Digest challenge so its next transfer answers it, as curl's authhost state does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-18
completed:
---
# BL-2747 — Keep a Libcurl handle's last Digest challenge so its next transfer answers it, as curl's authhost state does

## Goal

A Libcurl easy handle whose transfer got a 401 carrying a `WWW-Authenticate: Digest` challenge answers that challenge on the handle's next transfer when `CURLOPT_HTTPAUTH` then allows Digest, even though Digest was not allowed when the challenge arrived - as curl 8.22.0 does.

## Context

Found by BL-2691 (upstream case 2024, `lib2023 basic digest`): request 100 goes out with Basic and the wrong password and gets a 401 offering Digest (`nonce="1"`) and Basic; request 200, on the same handle with `CURLOPT_HTTPAUTH` = `CURLAUTH_DIGEST` and the right password, must send `Authorization: Digest username="testuser", realm="testrealm", nonce="1", uri="/20240200", response="ed646c565f79e2dd9fa37cb5a621213c"`. Curl sends no Authorization at all.

How curl does it (8.22.0 `lib/http.c`): `Curl_http_input_auth` calls `Curl_input_digest` on every Digest challenge "even if Digest authentication is not enabled", storing the nonce in `data->state.digest`, which lives on the easy handle across transfers. On the next transfer `Curl_pretransfer` sets `authhost.want = set.httpauth` and `picked &= want`; `Curl_http_output_auth` then picks the single wanted scheme (`picked = want`) and `output_auth_headers` builds the Digest answer from the stored state before any new challenge.

Where Curl stands: `Curl.Authentication.UnitLibrary/DigestAuthenticator.cs` is stateless (ADR-0014) and answers only challenges in the current transfer's responses; `Curl.Engine.UnitLibrary/TransferInfoFromReport.cs` already reads a transfer report's `WWW-Authenticate` headers (for `CURLINFO_HTTPAUTH_AVAIL`), so the challenges are reachable after a transfer, but no hook hands them to the next one. Needed: the handle (Libcurl `CurlEasy`, or the engine's per-handle state) keeps the last 401's challenges; the engine passes them in `HttpRequestOptions` (a new field in `Curl.Protocol.Abstractions.UnitLibrary`); `HttpProtocolHandler` answers them on the first request when the allowed schemes pick Digest. Record the design in an ADR (amending ADR-0014's no-state rule for this one case). Case 2030 (BL-2744, Digest after NTLM) and the second half of case 2024 (requests 300 to 500, nonces 2 and 3 on a fresh handle) exercise the same path.

## Acceptance criteria

- [ ] A Libcurl unit test in `Libcurl.UnitTests` performs two transfers on one handle against a fake server: the first under `CURLAUTH_BASIC` gets a 401 offering Digest, the second under `CURLAUTH_DIGEST` sends the Digest `Authorization` built from that challenge's nonce on its first request.
- [ ] `HttpProtocolHandler` answers prior challenges from `HttpRequestOptions` on its first request, pinned by a test in `Curl.Protocol.Http.UnitTests`; with no prior challenges its requests are unchanged.
- [ ] Every changed library passes `Measure-CodeQuality.ps1` (100% line and branch coverage, complexity at most 10).
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records where the state lives and why.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 880730e0 b0daa4a0 06081716 9319789a 3d836d5e.
