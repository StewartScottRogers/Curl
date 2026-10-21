---
id: BL-2856
title: Run the cases that need a Curl without a feature: 375 (no proxy), 1454 (no IPv6), 959, 960 and 961 (no IDN), and 445 (ldap)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2981, BL-2982, BL-2983]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitLibrary/UpstreamServedProtocolFeatures.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.MissingFeatureCases.cs, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitLibrary/UpstreamCurlPlatform.cs, Curl.Conformance.UnitLibrary/UpstreamCaseScreening.cs, Curl.Conformance.UnitTests/UpstreamCurlPlatformTests.Ldap.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.DebugStandIns.cs]
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

- 2026-10-21 (lane 2, sizing pass before any edit): too big for one run, and it reaches code outside this task's projects. The mask must reach the curl tool through `CurlComposition.CreateRunner` (`Curl.Console`, held by BL-2938 in Doing); the IPv6 refusal belongs where `--connect-to` is resolved (`Curl.Networking.UnitLibrary/ConnectDestination.cs`); the IDN switch belongs in `Curl.Protocol.Smtp.UnitLibrary/SmtpMailbox.cs`, which punycodes the mailbox host with `IdnMapping`. Filed BL-2981 (the mask in the engine and the tool, the ADR, and the `--proxy` refusal for 375), BL-2982 (IPv6 literal under the mask, 1454) and BL-2983 (IDN under the mask, 959-961), and made them `depends-on`. Together they meet this task's ADR and engine-unit-test criteria.
- What is left here once they are Done: report `ldap` (Curl's `curl -V` lists `ldap` and `ldaps` on every platform, `ManagedCurlVersion`) in `UpstreamServedProtocolFeatures` with a pin in `UpstreamCurlPlatformTests.Ldap.cs`, and check that 445's `rtsp` feature is reported too (no feature table names `rtsp` today); add `UpstreamCurlPlatform.WithoutFeatures` and have the conformance tests, for a case whose `<features>` list `!proxy`, `!IPv6` or `!IDN`, take those features off the platform (`PlatformFor`, in `UpstreamConformanceTests.DebugStandIns.cs`) and pass the mask to `CreateRunner` in `RunCurlAsync` (`UpstreamConformanceTests.cs`, also in BL-2865's touches - the board orders them). `FindFeatureOfAnotherPlatform` must count a feature the harness takes off as absent on every platform, so 959-961 (`!win32`) are `SkippedOnThisPlatform` on Windows and run on Linux and macOS. Added those files to `touches`.

## Log

- 2026-10-19: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 78dd162e edb7b05e f29424e7 296deadb dd1cc079 620ebead.
- 2026-10-21: Doing -> Backlog. Needs BL-2981 (feature mask in engine and tool, Curl.Console held by BL-2938), BL-2982 (IPv6) and BL-2983 (IDN) first
