---
id: BL-2660
title: Send Digest Proxy-Authorization for CURLOPT_PROXYAUTH and again after a proxy change (case 1588)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2660 — Send Digest Proxy-Authorization for CURLOPT_PROXYAUTH and again after a proxy change (case 1588)

## Goal

`CURLOPT_PROXYAUTH` `CURLAUTH_DIGEST` with `CURLOPT_PROXYUSERPWD` answers a proxy's 407 with a Digest `Proxy-Authorization`, and does so again on the same handle after `CURLOPT_PROXY` changes, so upstream case 1588 (lib1588) passes.

## Context

Found by BL-2542, which ported lib1588. The runner reports `differs at byte 217 (line 8): expected "Proxy-Authorization: Digest username=\"silly\", realm=\"weirdorealm\", nonce=\"12345\", uri=\"/path/1588\", response=\"d0b2f000c7e3fca24452b5810713404a\"\r\n", got "Accept: */*\r\n"`: no Digest answer to the 407 is sent through Libcurl.

## Acceptance criteria

- [x] Upstream case 1588 passes through the runner and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project covers the proxy Digest answer (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Cause: `ManagedEasyTransfer.TransferSettingsOf` built a library handle's settings with `ProxyCredentials: null`, so `CURLOPT_PROXYUSERPWD` never reached the proxy selector and the retry after the 407 went without `Proxy-Authorization`. The HTTP handler's Digest answer itself already worked (the tool's `--proxy-digest -U` cases pass), so only `Curl.Engine.UnitLibrary` changed; `Libcurl` needed nothing.
- Fix: `ManagedEasyTransfer.ProxyCredentialsOf` (ADR-0677): `CURLOPT_PROXYUSERPWD` split at its first colon, `CURLOPT_PROXYUSERNAME` and `CURLOPT_PROXYPASSWORD` each winning over their half (the record keeps no setopt order, so the separate options always win; recorded as the default choice in the ADR).
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: 100% line and branch, 0 failing members (a first version scored complexity 16 and was split into three methods). Fast tests green; case 1588 passes and is on the ratchet.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 63bf80fa 5b1dc09e 3d836d5e.
- 2026-10-17: Doing -> Done. A library handle's CURLOPT_PROXYUSERPWD now reaches the proxy, so a proxy's Digest 407 is answered, again after CURLOPT_PROXY changes; upstream case 1588 passes
