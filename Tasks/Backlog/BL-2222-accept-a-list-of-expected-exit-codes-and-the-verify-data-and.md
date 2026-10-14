---
id: BL-2222
title: Accept a list of expected exit codes and the <verify><data> and <verify><socks> parts in the upstream case runner (cases 301, 302, 305, 404, 405, 719, 720, 721)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-14
completed:
---
# BL-2222 — Accept a list of expected exit codes and the <verify><data> and <verify><socks> parts in the upstream case runner (cases 301, 302, 305, 404, 405, 719, 720, 721)

## Goal

Make the runner accept a comma-separated list of expected exit codes (`77,60`, `35,28`), the `<verify><data>` part, and the `<verify><socks>` part, so cases 301, 302, 305, 404, 405, 719, 720 and 721 run.

## Context

Source: Stewart's 2026-10-14 request to raise the gap office's behaviour score (aim 97-99%) by making the shared in-process harness run cases it leaves unmeasured; the 8.22.0 gap run 2026-10-13_1406 had 38 `harness-unsupported` cases, and running `Curl.Conformance.UnitTests` on 2026-10-14 names these eight. The gap tool uses `Curl.Conformance.UnitLibrary`.

- `UpstreamCaseScreening.UnsupportedErrorCode` skips "the expected exit code 77,60 is not a number" (305 https, 404 ftps) and "35,28" (405 ftp). In `runtests.pl` (curl-8_22_0) a `<errorcode>` with commas lists accepted codes (check how it compares); accept any listed code.
- `VerifyParts` omits `data`: 301 and 302 (https) hold `<verify><data>` with the placeholder `[insert full protocol verifiction dump here]`; read how `runtests.pl` treats a `<verify><data>` (it is not a stdout comparison in these cases) and ignore or compare it accordingly.
- `<verify><socks>` (719, 720, 721, server `socks5`) is the target socksd logs. `SocksServerConnector` (BL-1898) already handles the handshake; record the line socksd writes to its log (`tests/server/socksd.c`, curl-8_22_0) and compare it with `<verify><socks>`.

## Acceptance criteria

- [ ] An expected exit code list passes when curl's exit code is any one of the listed numbers and fails otherwise; `UpstreamCaseVerificationTests` pins both, and `UnsupportedErrorCode` rejects only a non-numeric entry.
- [ ] `<verify><data>` is handled as `runtests.pl` handles it; a test pins it for 301's placeholder form.
- [ ] `SocksServerConnector` exposes what socksd would log and the runner compares it with `<verify><socks>`; unit tests in `SocksServerConnectorTests` and `UpstreamCaseVerificationTests`.
- [ ] Cases 301, 302, 305, 404, 405, 719, 720 and 721 are no longer screened out. Each that passes is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; each that runs but fails is named in Notes as a real gap, with a follow-up task per distinct cause.
- [ ] Platform-neutral tests; `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` shows no member outside a gate; `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` is green; `Curl.Conformance.UnitLibrary/CLAUDE.md` is updated.

## Notes

## Log

- 2026-10-14: Created.
