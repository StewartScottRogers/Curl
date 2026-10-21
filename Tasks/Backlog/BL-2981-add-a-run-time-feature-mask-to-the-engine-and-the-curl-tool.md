---
id: BL-2981
title: Add a run-time feature mask to the engine and the curl tool, off by default, and refuse --proxy under it as a build without proxy does (case 375)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Console/CurlComposition.cs, Curl.Console.UnitTests/CurlCompositionFeatureMaskTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2981 — Add a run-time feature mask to the engine and the curl tool, off by default, and refuse --proxy under it as a build without proxy does (case 375)

## Goal

Curl's engine and `CurlComposition.CreateRunner` take an optional run-time feature mask (off by default) naming features to behave as built without, and under a mask without `proxy` the tool answers `-x wohoo http://example.com/` with `curl: proxy support is disabled in this libcurl` and exit 4, as upstream case 375 expects.

## Context

Split from BL-2856 (which runs cases 375, 1454, 959-961 and 445 in the harness and lists them). Upstream builds libcurl without a feature for these cases; Curl is one build, so the harness needs a mask it can switch on per case. BL-2856's Context describes the design: the mask lives in `Curl.Engine.UnitLibrary`, is off by default so the shipped tool and `Libcurl` are unchanged, and the affected code answers `CURLE_NOT_BUILT_IN` as a build without the feature does. BL-2982 (IPv6) and BL-2983 (IDN) build on this mask.

- curl 8.22.0 `src/tool_operate.c`: when setting `CURLOPT_PROXY` fails (a build without proxy), the tool writes `proxy support is disabled in this libcurl` and returns `CURLE_NOT_BUILT_IN`. Measure the stderr text against case 375's `<verify><stderr>`.
- `CreateRunner` gains one optional parameter carrying the mask; `Curl.Conformance.UnitTests`' `RunCurlAsync` passes it in BL-2856, not here.

## Acceptance criteria

- [ ] One ADR, marked "Decided by Claude under Stewart's delegation", names the mask type, where it lives, which features it can hide (`proxy`, `IPv6`, `IDN`), and states that neither the shipped curl tool nor `Libcurl` enables it by default.
- [ ] `Curl.Engine.UnitTests` shows that with the mask hiding `proxy` a transfer with a proxy set answers `CURLE_NOT_BUILT_IN`, and without the mask nothing changes.
- [ ] A test in `Curl.Console.UnitTests` shows `CreateRunner` with the mask hiding `proxy` runs `-x wohoo http://example.com/` to exit 4 with stderr `curl: proxy support is disabled in this libcurl` and a line feed, and without the mask the run is unchanged.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
