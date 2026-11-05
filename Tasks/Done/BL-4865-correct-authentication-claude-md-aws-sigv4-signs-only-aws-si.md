---
id: BL-4865
title: Correct Authentication CLAUDE.md: AWS SigV4 signs only --aws-sigv4 requests, not every HTTP transfer
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Authentication.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4865 — Correct Authentication CLAUDE.md: AWS SigV4 signs only --aws-sigv4 requests, not every HTTP transfer

## Goal

`Curl.Authentication.UnitLibrary/CLAUDE.md` says truthfully when AWS Signature Version 4 signing happens: only for a request that carries `--aws-sigv4`.

## Context

Found by the shift-end review of 2026-11-04 (pr-review-toolkit code-reviewer, diff `7051feb90...c0032951f`).

`Curl.Authentication.UnitLibrary/CLAUDE.md:64-65` now reads: "`EngineComposition` wires it into every HTTP transfer: `AwsSigV4HttpAuthenticator` (in `Curl.Engine.UnitLibrary`) signs the request with an `AwsSigV4Signer` (BL-629)."

The code does something narrower. `EngineComposition.cs:180` puts `AwsSigV4HttpAuthenticator` in front of every HTTP handler's authenticator. But `AwsSigV4HttpAuthenticator.CreateAuthorization` and `CreateAuthorizationAsync` (`Curl.Engine.UnitLibrary/AwsSigV4HttpAuthenticator.cs:33-48`) sign only when `request.AwsSigV4 is { } inputs`. Every other request is handed to `otherSchemes`, as the class's own summary says.

As written, an agent reading the file would believe every HTTP transfer is SigV4-signed. That breaks the root CLAUDE.md rule "say what it does, do what it says".

## Acceptance criteria

- [x] `Curl.Authentication.UnitLibrary/CLAUDE.md` says that `EngineComposition` puts `AwsSigV4HttpAuthenticator` (in `Curl.Engine.UnitLibrary`) in front of every HTTP transfer's authenticator.
- [x] It says the authenticator signs a request with `AwsSigV4Signer` only when `--aws-sigv4` is given (`HttpAuthRequest.AwsSigV4` set), and passes every other request to the other schemes.
- [x] No other line of the file changes meaning.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: 7e952c3f 97c5d10c 4d0f720b.
- 2026-11-04: Doing -> Done. Authentication CLAUDE.md now says SigV4 signs only --aws-sigv4 requests
