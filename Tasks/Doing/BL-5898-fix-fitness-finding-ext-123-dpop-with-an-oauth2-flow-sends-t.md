---
id: BL-5898
title: Fix fitness finding EXT-123: --dpop with an --oauth2-* flow sends the flow's token as Authorization: Bearer, with a DPoP proof that has no ath
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.OAuth2.UnitLibrary/Options/OAuth2TransferHook.cs, Curl.Extension.Dpop.UnitLibrary/DpopProof.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5898 — Fix fitness finding EXT-123: --dpop with an --oauth2-* flow sends the flow's token as Authorization: Bearer, with a DPoP proof that has no ath

## Goal

EXT-123 from the outside fitness review of 2026-11-24 13:58 no longer reproduces: --dpop with an --oauth2-* flow sends the flow's token as Authorization: Bearer, with a DPoP proof that has no ath. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T1358` (commit `1e5d591`, the `preview` release binaries). That review was a deeper second pass of the same commit as `Curl-Fitness-Review-2026-11-24T0708`, covering areas the earlier pass did not test.

This is an extension (Part B): it is judged against Curl's own documentation (`curl --extended-help`, `Documentation/Protocols.md`, `Documentation/Conformance/Extended/*.md`, the ADRs), never against C curl.

The reviewer's mock servers were Python, and some findings used real servers in Docker (Dovecot, GreenMail, Samba, Mosquitto, Kafka, Prosody, SeaweedFS, Azurite); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Medium (the token is never key-bound, defeating the option's purpose) · **Confidence:** Confirmed (2 runs) · **Area:** `--dpop` + OAuth2 flows. Windows build.
- **Setup:** a mock OAuth2 server on 127.0.0.1:37100 with a device-authorization endpoint `/device`, a token endpoint `/token` (issues `access_token=ACC-DEVICE`, `token_type=Bearer`) and resources `/res*` that log the `Authorization` and `DPoP` headers; `openssl ecparam -name prime256v1 -genkey -noout -out dpop.pem`.
- **Repro:** `curl -sS --dpop dpop.pem --oauth2-device http://127.0.0.1:37100/token --oauth2-device-url http://127.0.0.1:37100/device --oauth2-client-id cid --oauth2-client-secret csec --oauth2-token-cache tc http://127.0.0.1:37100/res6`
- **Expected:** `--extended-help` says `--dpop` "binds OAuth 2.0 access tokens to a key … sends --oauth2-bearer as Authorization: DPoP"; ADR-1145 says the proof carries `ath` "when an access token is sent"; RFC 9449 §4.2 makes `ath` mandatory when a token is presented. So the request should carry `Authorization: DPoP ACC-DEVICE` and a proof with `ath` (and, per RFC 9449 §5, the token request itself should carry a proof), or the combination should be refused with a clear error.
- **Actual (.NET):** the resource sees `Authorization: Bearer ACC-DEVICE` and a `DPoP` proof whose payload is `{"jti":…,"htm":"GET","htu":"http://127.0.0.1:37100/res6","iat":…}` with no `ath`; the token request carries no proof. Exit 0. Control: `--dpop dpop.pem --oauth2-bearer XTOK` correctly sends `Authorization: DPoP XTOK` with `ath`. A server that issues `token_type: DPoP` is refused with exit 8.
- **Likely location:** `Curl.Extension.OAuth2.UnitLibrary/Options/OAuth2TransferHook.cs` (hard-coded `Authorization: Bearer`; `token_type` must be `Bearer`) and `Curl.Extension.Dpop.UnitLibrary/DpopProof.cs` (computes `ath` only for `--oauth2-bearer` or a `-H Authorization: DPoP` header).
- **Acceptance check:** the repro logs `Authorization: DPoP ACC-DEVICE` and a DPoP payload containing `"ath":"<base64url SHA-256 of ACC-DEVICE>"`; or the command fails with a message naming the unsupported combination.

## Acceptance criteria

- [ ] the repro logs `Authorization: DPoP ACC-DEVICE` and a DPoP payload containing `"ath":"<base64url SHA-256 of ACC-DEVICE>"`; or the command fails with a message naming the unsupported combination.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 37c06683 69116afe 3d836d5e.
