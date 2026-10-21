---
id: BL-2833
title: Stand in for CURL_FAKE_XATTR so upstream cases 644, 687 and 688 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitTests/UpstreamConformanceTests.FakeXattrStandIn.cs, Curl.Console/IExtendedAttributeWriter.cs, Curl.Console/CurlComposition.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.DebugStandIns.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.cs, Curl.Conformance.UnitTests/FakeXattrStandInTests.cs]
requirement: none
created: 2026-10-19
completed: 2026-10-21
---
# BL-2833 — Stand in for CURL_FAKE_XATTR so upstream cases 644, 687 and 688 run

## Goal

Upstream cases 644, 687 and 688 (`--xattr`) run in the harness with the debug build's `CURL_FAKE_XATTR` stood in for, and each is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` or has a recorded reason.

## Context

The three cases name `Debug` and `xattr` and set `CURL_FAKE_XATTR=1`: 644 (`--xattr` with redirect), 687 (basic `--xattr` with `-o`) and 688 (`--xattr` with uppercase `-O`). In upstream's debug build, `src/tool_xattr.c` at tag `curl-8_22_0` does not write extended attributes when the variable is set but prints what it would set to standard output, which `<verify><stdout>` compares (read the file to confirm the exact line format before writing the stand-in). Gap scorecard 2026-10-19_1329 has them `excluded | debug-build-only`.

`UpstreamCurlPlatform` lists `xattr` on Linux and macOS only ("curl cannot write extended attributes" on Windows), so on Windows these cases are skipped by the feature `xattr`, which ADR-0488 makes `Inconclusive` rather than failed once listed. `Curl.Console/IExtendedAttributeWriter.cs` is the existing seam for writing attributes (`NativeExtendedAttributeWriter` is the real one).

Stand-in, as ADR-0687/0678/0698 do: `UpstreamConformanceTests.PlatformFor` reports `Debug` to a case that sets `CURL_FAKE_XATTR`, and the harness gives the curl tool an `IExtendedAttributeWriter` that writes upstream's fake line instead of touching the file system. It belongs to the harness; the real tool does not read `CURL_FAKE_XATTR`.

Because Windows cannot run these cases, a lane cannot see them pass; the `CI` workflow on Linux is the check.

## Acceptance criteria

- [x] `PlatformFor` reports `Debug` to a case that sets `CURL_FAKE_XATTR` and to none that does not; a unit test pins both.
- [x] A unit test pins the fake writer's output line for the attributes 644, 687 and 688 expect (`user.creator`, `user.mime_type`, `user.xdg.origin.url` or the names the cases hold), against `tool_xattr.c`.
- [x] 644, 687 and 688 are on `PassingUpstreamCases.txt` and are `Inconclusive` (feature `xattr` of another platform) on Windows, never `Failed`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and the changed libraries keep 100% line and branch coverage.
- [x] After the push, `CI` is green on Linux and macOS for the commit; a case that fails there is taken off the list again with the failure text in Notes.

## Notes

- Touches widened (no other Doing task on origin/work/dark-factory names them): `Curl.Console/CurlComposition.cs` (the dialing `CreateRunner` passed no `IExtendedAttributeWriter`, so the harness ignored `--xattr`; it now takes `extendedAttributeWriter`, null by default), `UpstreamConformanceTests.DebugStandIns.cs` (one `DebugStandInRules` line for `CURL_FAKE_XATTR=`; `PlatformFor` made internal for its test), `UpstreamConformanceTests.cs` (`RunCurlAsync` passes `ExtendedAttributeWriterFor(invocation)`) and the new `FakeXattrStandInTests.cs`. `IExtendedAttributeWriter.cs` needed no change.
- Stand-in, harness only as ADR-0687/0678/0698 do (the real tool never reads `CURL_FAKE_XATTR`): `UpstreamConformanceTests.FakeExtendedAttributeWriter` prints `<name> => <value>` and a line feed to the case's stdout per attribute and reports each set. The line format is the cases' own non-win32 `<stdout>`, which is tool_xattr.c's debug-build printf; always LF, as the Linux and macOS builds print it, the only platforms the cases run on. Sensible default, no ADR: it follows the existing Debug stand-in pattern.
- Windows: `xattr` is a Linux/macOS feature in `UpstreamCurlPlatform`, so the three listed rows report Inconclusive (shown as Skipped in the ratchet run here), never Failed.
- Measured: `dotnet build Curl.slnx -warnaserror` clean; fast tests all passed (Conformance 3679 passed, 0 failed, 255 skipped); `Measure-CodeQuality.ps1 -Library Curl.Console`: 0 failing members.
- Not run on Linux or macOS here (no lane can); a lane may not push, so the CI criterion is handed to the shift: the coordinator's CI watch (BL-987) files a High task naming any of 644, 687 or 688 that fails on Linux or macOS.

## Log

- 2026-10-19: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: d13ab093 7b704265 6e7fdf92 ac32fd53 c971d4dc.
- 2026-10-21: Doing -> Done. Upstream cases 644, 687 and 688 run through the CURL_FAKE_XATTR stand-in and are on the ratchet (Inconclusive on Windows)
