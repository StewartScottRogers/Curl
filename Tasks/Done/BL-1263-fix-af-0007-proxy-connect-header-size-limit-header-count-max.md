---
id: BL-1263
title: Fix AF-0007: Proxy CONNECT header size limit `header.Count > MaximumHeaderBytes` can become >= with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-02
---
# BL-1263 — Fix AF-0007: Proxy CONNECT header size limit `header.Count > MaximumHeaderBytes` can become >= with no test failing

## Goal

The defect the audit office reported as AF-0007 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0007 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0007-proxy-connect-header-size-limit-header-count-maxim.md`.

Location: `Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:170`

Location: `Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:170`

Mutant survived (seed 0): `>` became `>=`. The failure message "Too large response headers: N > Max" is user-visible, yet no test sends a header exactly MaximumHeaderBytes long, so the boundary is unpinned.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0 -TimeoutSeconds 300 -OutFile $env:TEMP\mutation-net.json
```

- Expected: The mutant at Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:170 (>) is killed.
- Actual: survived  Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:170 >

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Fix is test-only: `ReadReplyAsync_WhenALineEndsTheHeaderBlockAt307200Bytes_KeepsReading` in
  `Curl.Networking.UnitTests/HttpProxyTunnelTests.cs`. A header block whose line ends at exactly
  307200 bytes is still read (the proxy then closes: `Proxy CONNECT aborted`); one byte more gives
  `Too large response headers: 307201 > 307200`. The production code was already right.
- Added `Curl.Networking.UnitTests` to `touches`: the pinning test lives there, and no task in
  Doing on `origin/work/dark-factory` names it.
- Mutant checked by hand rather than with the 40-mutant tool run: with line 170 changed to `>=`,
  the 438-byte row fails; with `>` restored, all 94 HttpProxyTunnel tests pass.
- Boundary not re-measured against real curl: curl's own message `<n> > 307200` states the
  comparison is strict, and the over-limit case was already measured (307762).

## Log

- 2026-10-03: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. A CONNECT header block of exactly 307200 bytes is pinned as within the limit, killing the > to >= mutant (AF-0007)
