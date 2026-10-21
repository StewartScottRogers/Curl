---
id: BL-2856
title: Run the cases that need a Curl without a feature: 375 (no proxy), 1454 (no IPv6), 959, 960 and 961 (no IDN), and 445 (ldap)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitLibrary/UpstreamServedProtocolFeatures.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.MissingFeatureCases.cs, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-19
completed:
---
# BL-2856 — Run the cases that need a Curl without a feature: 375 (no proxy), 1454 (no IPv6), 959, 960 and 961 (no IDN), and 445 (ldap)

## Goal

Upstream cases 375, 1454, 959, 960, 961 and 445 run in the harness with Curl switched at run time into the feature set they name (no proxy, no IPv6, no IDN, and ldap reported), and each is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` or has a recorded reason.

## Context

Gap scorecard 2026-10-19_1329: 375 (`!proxy`), 1454 (`!IPv6`) and 445 (`ldap`) are `unmeasured | harness-unsupported`; 959, 960 and 961 (`!IDN,!win32,codeset-utf8`, SMTP with a UTF-8 sender host) are `excluded | reference-lacks:codeset-utf8`. Measured 2026-10-19 on Windows each is skipped: `the case needs Curl without the feature proxy` / `IPv6` / `IDN`, and `Curl lacks the feature ldap`.

- 375: `-x wohoo http://example.com/` must fail with `curl: proxy support is disabled in this libcurl` and exit 4 (`CURLE_NOT_BUILT_IN`).
- 1454: `--connect-to localhost:80:[::1]:80 localhost` against a Curl without IPv6 exits 4.
- 959, 960, 961: with `!IDN` an SMTP sender whose host part is non-ASCII fails with exit 55 (`CURLE_SEND_ERROR`) after `EHLO` and `MAIL FROM`, per `tests/data/test959`; read 960 and 961 for their own answers.
- 445 ("Refuse tunneling protocols through HTTP proxy") needs the `ldap` feature, which `UpstreamCurlPlatform` does not list although `Curl.Protocol.Ldap.UnitLibrary` exists (root CLAUDE.md: the Windows reference is WinLDAP, Linux OpenLDAP; `ADR-0021` lists only what Curl implements). Report `ldap` on the platforms where the reference curl reports it and run the case; its `<limits> Allocations: 1500` part is ignored by the harness (`VerifyParts` accepts `limits`).

`Libcurl` has the feature bits (`CurlVersion`, `CurlVersionFeatures`); upstream's tests build libcurl without a feature, while Curl is one build. The stand-in is a run-time feature mask: the harness passes a case's `!feature` lines to the engine so that, for that case only, the proxy, IPv6 or IDN code answers `CURLE_NOT_BUILT_IN` as a build without it does. Decide and record in one ADR ("Decided by Claude under Stewart's delegation", in the style of ADR-0687/0678) where the mask lives (an engine option in `Curl.Engine.UnitLibrary`, off by default so the shipped tool is unchanged) and which `CurlVersionFeatures` it hides. Add the folders you change to `touches` in the same commit.

## Acceptance criteria

- [ ] The ADR names the mask, where it lives, and states that neither the shipped curl tool nor `Libcurl` enables it by default.
- [ ] Unit tests in the engine's own `.UnitTests` project show that, with the mask, a proxy option, an IPv6 literal or an IDN host name answers `CURLE_NOT_BUILT_IN` (exit 4, or 55 for 959 as the case expects), and without the mask nothing changes.
- [ ] `UpstreamCurlPlatform` reports `ldap` and a unit test pins it.
- [ ] Cases 375, 1454, 959, 960, 961 and 445 each end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows (or `Inconclusive` where only another platform's features fit: 959 to 961 need `!win32`) and are on `PassingUpstreamCases.txt`, or have their first difference in Notes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.
- [ ] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

## Log

- 2026-10-19: Created.
