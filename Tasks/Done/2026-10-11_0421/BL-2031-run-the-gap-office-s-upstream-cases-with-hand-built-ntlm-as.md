---
id: BL-2031
title: Run the gap office's upstream cases with hand-built NTLM, as the conformance harness does (GF-0003)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-2031 — Run the gap office's upstream cases with hand-built NTLM, as the conformance harness does (GF-0003)

## Goal

The gap office's upstream-case measuring tool (`Measure-UpstreamCases.cs` in the gap office's
Tools folder) composes Curl with `CurlComposition.CreateRunner(..., usesHandBuiltNtlm: true)`, as
`Curl.Conformance.UnitTests/UpstreamConformanceTests.cs` does (ADR-0455), so its `!SSPI` NTLM cases
send curl's own type-1 message on Windows.

## Context

Interactive only: the tool lives under the gap office's folder, which lanes may not read or change
(ADR-0433). Found by BL-1999 (GF-0003 re-close). The gap run of 2026-10-10_0657 measured test67 with
SSPI's type-1 (`TlRMTVNTUAABAAAAB4IIogAAAAAAAAAAAAAAAAAAAAAKAPRlAAAADw==`, flags 0xa2088207 plus
VERSION), while the in-repo harness, which sets `usesHandBuiltNtlm: true`, passes test67, 68, 81, 89,
91, 150, 162, 822, 827, 831, 868, 873, 877, 906, 921, 933 and 1215, and since BL-1999 also 775 and
776. `UpstreamCurlPlatform.Windows` lists no `SSPI`, so the `!SSPI` cases run; with SSPI-routed NTLM
they cannot match.

Also stale: the summary of `Curl.Console/HandBuiltNtlmSecurityContextFactory.cs` says Curl's
`curl -V` lists no `SSPI`; since ADR-0439 it does on Windows. The harness runs Curl as the non-SSPI
build because `UpstreamCurlPlatform` lists no `SSPI`. Reword it.

## Acceptance criteria

- [x] The measuring tool passes `usesHandBuiltNtlm: true` to `CurlComposition.CreateRunner`.
- [x] Re-measuring 67,81,775 with the tool on Windows gives `match` for all three.
- [x] `HandBuiltNtlmSecurityContextFactory`'s summary says why the harness runs Curl as the non-SSPI build without claiming `curl -V` lists no SSPI.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- 2026-10-10 (interactive, gap PR #108 e52f2c111): criterion 1 is met through InProcessCurl, which passes usesHandBuiltNtlm: true on every overload since BL-2033; CurlComposition.CreateRunner is internal and the tool goes only through InProcessCurl. 67, 81 and 775 measure match (so do 68, 89, 91, 150, 162, 822, 827, 831, 868, 873, 877, 906, 921, 933, 1215 and 776). Left for a lane, product code only: reword lines 8-10 of the summary in Curl.Console/HandBuiltNtlmSecurityContextFactory.cs, e.g. 'the harness runs Curl as the non-SSPI build because the harness's platform feature list (UpstreamCurlPlatform) has no SSPI, so the !SSPI NTLM cases run'. lane: no removed.

- 2026-10-10 (lane 1): reworded the summary: the harness runs Curl as the non-SSPI build because UpstreamCurlPlatform has no SSPI, while Curl's own curl -V lists SSPI on Windows (ADR-0439). Build clean; Curl.Console.UnitTests 2785 passed.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. Summary reworded; tool criteria met interactively
