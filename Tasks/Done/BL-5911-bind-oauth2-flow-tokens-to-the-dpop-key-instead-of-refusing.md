---
id: BL-5911
title: Bind --oauth2-* flow tokens to the --dpop key instead of refusing the combination (ADR-1347)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Abstractions.UnitLibrary/Transfer, Curl.Extension.Abstractions.UnitLibrary/CLAUDE.md, Curl.Extension.Abstractions.UnitTests/Transfer, Curl.Extension.OAuth2.UnitLibrary, Curl.Extension.OAuth2.UnitTests, Curl.Extension.Dpop.UnitLibrary, Curl.Extension.Dpop.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs, Curl.Extension.Composition.UnitTests/DpopWithOAuth2OrderTests.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5911 — Bind --oauth2-* flow tokens to the --dpop key instead of refusing the combination (ADR-1347)

## Goal

`--dpop` with an `--oauth2-*` flow gets a token bound to the DPoP key and sends it as `Authorization: DPoP` with a proof that holds its `ath`, instead of failing with exit 2.

## Context

BL-5898 (fitness finding EXT-123) made the combination fail closed: ADR-1347, `OAuth2TransferHook.DpopProofMessage`. ADR-1347 says this task builds the binding and replaces that refusal. Needed (RFC 9449): a proof on the token and refresh requests (§5), with the token endpoint's `DPoP-Nonce` retried (§8); `token_type` `DPoP` accepted, and the cache (`Curl.Extension.OAuth2.UnitLibrary/Cache`, ADR-1034) keeping a bound token apart from a Bearer one and from one bound to another key; `Authorization: DPoP <token>` and `ath` on the resource request, the nonce retry and followed hops included (§4.2, §7.1). The two extensions may not reference each other, so the seam goes in `Curl.Extension.Abstractions.UnitLibrary/Transfer`; today `--dpop`'s hook runs before the OAuth2 hook and makes its proof before the token exists. Update ADR-1145, ADR-1347 (superseded), both libraries' `CLAUDE.md`, and `DpopExtension.Description`, which says the combination is not yet supported.

## Acceptance criteria

- [x] `--dpop <key> --oauth2-device <url> ...` against a mock server that issues `token_type` `DPoP` sends a proof on the token request, then `Authorization: DPoP <token>` with a proof whose `ath` is the base64url SHA-256 of the token; unit tests pin both requests.
- [x] A cached token is reused only with the key it was bound to, pinned by a unit test.
- [x] `--extended-help` no longer says the combination fails.
- [x] `dotnet build` is clean, the fast tests pass and `Measure-CodeQuality.ps1` shows no failing member in the changed libraries.

## Notes

- Design (ADR-1356, supersedes ADR-1347): the OAuth2 hook now runs before `--dpop`'s, so the existing DPoP hook signs
  the request carrying the token (`ath`, nonce retry, followed hops) with one change: an `Authorization: DPoP` header's
  token wins over `--oauth2-bearer`'s. The key is lent through `IDpopKeySource` / `TransferHookServices.DpopKeySource`.
- With a key only `token_type` `DPoP` is accepted (fail closed, exit 8). `-H "DPoP: ..."` beside a flow stays exit 2.
- `touches` widened: `ExtensionTransferHookCatalogue.cs` (the order), a test file in `Abstractions.UnitTests/Transfer`
  and Abstractions' `CLAUDE.md`. No other task in Doing names them.
- Tests: `DpopWithOAuth2OrderTests` (device flow end to end, both requests), `OAuth2TransferHookDpopTests` (cache per
  key, nonce, refresh), `DpopKeySourceTests`, `TransferHookingProtocolHandlerTests.DpopKeySource.cs`.
- Verified: `dotnet build -warnaserror` clean; whole fast test run passed (one `DataRow` added afterwards, its project
  rerun); `dotnet format --verify-no-changes` on the changed files; `--extended-help` read.
- Quality: the `-Library` run reaches most test projects and was still running after 48 minutes, so it was stopped and
  the four libraries scored with `-SkipTestRun` over coverage from their own four test projects and
  `Curl.Console.UnitTests`: 100% line and branch, 0 failing members.
- Criterion on Linux and macOS: the new tests use only temporary files and platform-neutral URLs.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing. Criteria at claim: c013d3b8 a66d0330 0ff5c1a5 0fba324b.
- 2026-11-24: Doing -> Done. --dpop with an --oauth2-* flow now gets a token bound to the DPoP key and sends it as Authorization: DPoP with its ath
