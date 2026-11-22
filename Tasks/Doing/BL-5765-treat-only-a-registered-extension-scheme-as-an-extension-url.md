---
id: BL-5765
title: Treat only a registered extension scheme as an extension URL so a schemeless read-callback POST keeps its body
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.Mqtt.cs]
requirement: none
created: 2026-11-22
completed:
---
# BL-5765 — Treat only a registered extension scheme as an extension URL so a schemeless read-callback POST keeps its body

## Goal

A library handle's `CURLOPT_POST` with a `CURLOPT_READFUNCTION` to a URL with no scheme, such as `localhost:8080/x`, sends the callback's whole body over HTTP. The engine counts that URL as an extension URL today.

## Context

The shift-end review of 2026-11-22 (ADR-0710) found this in the merge `dfb9516dda...9e2c6390ba`. The type-design, silent-failure and code reviewers each reported it on their own.

- **The bug:** `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs:317` `IsExtensionUrl` (BL-5758, BL-5754) takes the raw `options.Network.Url` text before its first `:`. It calls the URL an extension URL whenever that text is not in `ManagedCurlVersion.Protocols`.
- **Which URLs it catches:**
  - A URL with no scheme, which libcurl treats as `http`: `localhost:8080/x` gives `localhost`, `user:pw@host/` gives `user`, `[::1]:8080/` gives `[`.
  - A mistyped scheme such as `htp://`.
- **For such a URL:**
  - `ReadCallbackPostDataOf` (line 280) reads the read callback to its end while `TransferSettingsOf` builds the settings (`ManagedEasyTransfer.Perform.cs:243`).
  - Then `ReadCallbackPostBodyOf` (`ManagedEasyTransfer.PostBody.cs:69`) wraps the same callback for the HTTP body, gets 0 at once, and the POST goes out empty or truncated.
  - `CURLOPT_POSTFIELDS` is also copied onto `PostData` for what is an HTTP transfer.
- **What libcurl does:** it guesses `http` for these URLs and streams the callback once.
- **A mistyped scheme:** the callback runs, with its side effects, for a transfer that then fails with exit 1.
- **Where to start:** the engine may not reference `Curl.Extension.Composition` (the engine sits beneath the hosts). So decide on the parsed `CurlUrl` (whose `Scheme` is the guessed `http` for a URL with no scheme, as `HoldsUploadToDeclaredLength` already uses). At minimum, require a real scheme (RFC 3986 scheme characters followed by `:`) that is neither a curl protocol nor missing.
- **ADRs:** ADR-1336 and ADR-1337 describe the behaviour this task keeps for real extension schemes.

## Acceptance criteria

- [x] A `ManagedEasyTransferTests` case posts through a read callback to `localhost:<port>/x` (no scheme) on a scripted connector. It asserts that the HTTP request carries the callback's full body and that the callback was read once.
- [x] A case with `CURLOPT_POSTFIELDS` to a URL with no scheme asserts that the context's `PostData` is `null`.
- [x] The existing extension cases in `ManagedEasyTransferTests.Mqtt.cs` (BL-5754, BL-5758) still pass. `mcp://`, `mcp+stdio:`, `nats://` and the other extension schemes still get their body as `PostData`.
- [x] `dotnet build` is clean, the fast tests pass, and `Curl.Engine.UnitLibrary` keeps its four quality gates.

## Notes

- Done: `IsExtensionUrl` now needs an explicit scheme (`ExplicitSchemeOf`: RFC 3986 characters then `://`, or a `+` in the scheme as `mcp+stdio:`), so `localhost:8080/x`, `user:pw@host/` and `[::1]:8080/` are HTTP. Engine quality gates pass; fast tests green.
- Unmet, split out: the `htp://host/` criterion (exit 1, callback never read). Syntax cannot tell `htp://` from `nats://`; only the extension handlers' registered schemes can, and the engine sees them only inside `ManagedCurlEngine`. Filed as BL-5768. That criterion moved to BL-5768.

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: 331bd101 e2736da1 20ec790b bcef008d da8c1361.
