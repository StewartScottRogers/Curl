---
id: BL-5903
title: Bind --oauth2-* flow tokens to the --dpop key instead of refusing the combination (ADR-1347)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Abstractions.UnitLibrary/Transfer, Curl.Extension.OAuth2.UnitLibrary, Curl.Extension.OAuth2.UnitTests, Curl.Extension.Dpop.UnitLibrary, Curl.Extension.Dpop.UnitTests, Curl.Extension.Composition.UnitTests/DpopWithOAuth2OrderTests.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5903 — Bind --oauth2-* flow tokens to the --dpop key instead of refusing the combination (ADR-1347)

## Goal

`--dpop` with an `--oauth2-*` flow gets a token bound to the DPoP key and sends it as `Authorization: DPoP` with a proof that holds its `ath`, instead of failing with exit 2.

## Context

BL-5898 (fitness finding EXT-123) made the combination fail closed: ADR-1347, `OAuth2TransferHook.DpopProofMessage`. ADR-1347 says this task builds the binding and replaces that refusal. Needed (RFC 9449): a proof on the token and refresh requests (§5), with the token endpoint's `DPoP-Nonce` retried (§8); `token_type` `DPoP` accepted, and the cache (`Curl.Extension.OAuth2.UnitLibrary/Cache`, ADR-1034) keeping a bound token apart from a Bearer one and from one bound to another key; `Authorization: DPoP <token>` and `ath` on the resource request, the nonce retry and followed hops included (§4.2, §7.1). The two extensions may not reference each other, so the seam goes in `Curl.Extension.Abstractions.UnitLibrary/Transfer`; today `--dpop`'s hook runs before the OAuth2 hook and makes its proof before the token exists. Update ADR-1145, ADR-1347 (superseded), both libraries' `CLAUDE.md`, and `DpopExtension.Description`, which says the combination is not yet supported.

## Acceptance criteria

- [ ] `--dpop <key> --oauth2-device <url> ...` against a mock server that issues `token_type` `DPoP` sends a proof on the token request, then `Authorization: DPoP <token>` with a proof whose `ath` is the base64url SHA-256 of the token; unit tests pin both requests.
- [ ] A cached token is reused only with the key it was bound to, pinned by a unit test.
- [ ] `--extended-help` no longer says the combination fails.
- [ ] `dotnet build` is clean, the fast tests pass and `Measure-CodeQuality.ps1` shows no failing member in the changed libraries.

## Notes

## Log

- 2026-11-24: Created.
