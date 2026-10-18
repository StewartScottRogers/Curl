# ADR-0680 — Upstream case 558 stays excluded: the harness never reports `TrackMemory`

- **Status:** Accepted
- **Date:** 2026-10-18

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2615.

## Context

Upstream case 558 ("libtest memory tracking operational") names the `TrackMemory` feature and runs `lib558`, which
Curl ports as `Curl.Conformance.LibTest.UnitLibrary/Lib558.cs` (BL-2531). Its only check is the `<file>` verify of
`%LOGDIR/memdump`: the log upstream's memory-debug build (`CURLDEBUG`, `lib/memdebug.c`) writes for every
allocation, which after the case's `<stripfile>` must read

```
MEM easy.c: malloc()
MEM lib558.c: malloc()
MEM lib558.c: free()
MEM dynbuf.c: realloc()
MEM dynbuf.c: realloc()
MEM escape.c: free()
MEM easy.c: free()
```

Those lines name the C source files of libcurl's allocator calls in `curl_easy_init`, `curlx_dynbuf` and
`curl_easy_escape`. Curl is managed code: it has no `malloc`, `realloc` or `free` to wrap, no C source files to
name, and its memory is the garbage collector's. Upstream's own release builds lack `TrackMemory` and skip the case
the same way (`runtests.pl` derives the feature from `curl -V`), and Curl's `curl -V` matches those builds
(ADR-0021). ADR-0420 already keeps `TrackMemory` off the harness's feature list.

## Decision

- The harness never reports `TrackMemory`, to case 558 or any other: `UpstreamCurlPlatform` leaves it off on both
  platforms (pinned by `UpstreamCurlPlatformTests.BothPlatforms_LeaveOffWhatCurlLacks`), and
  `UpstreamConformanceTests.PlatformFor` has no stand-in for it, unlike the `Debug` stand-ins of ADR-0671 and
  ADR-0678.
- Case 558 stays skipped as `Curl lacks the feature TrackMemory` and off `PassingUpstreamCases.txt`.
- The harness does not fabricate a `memdump`. Writing the seven expected lines from the case itself would make the
  case pass while checking nothing Curl does; the stand-ins of ADR-0671 and ADR-0678 are kept only because there
  Curl's own output is still what the case compares.

## Consequences

- `Lib558.cs` stays ported and its escape result is covered by `Lib558And559Tests.RunAsync_Lib558_EscapesAndPrintsNothing`; only the memory log goes
  unchecked.
- The skip is the exclusion, with this ADR as its reason; a gap run records 558 as `excluded`, `debug-build-only`
  (`Gap/Instructions/Gap-Format.md`).
