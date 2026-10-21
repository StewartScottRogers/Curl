# ADR-0722: Upstream cases 96, 506, 518, 537 and 558 stay off; 1592 runs on a stalled-lookup stand-in

- Status: Accepted
- Date: 2026-10-21
- Decided by Claude under Stewart's delegation (BL-2839)

## Context

On 2026-10-19 the gap scorecard skipped six upstream cases for their environment rather than for a Curl
difference: 96 and 558 for `TrackMemory`, 506 for `!threaded-resolver`, 518 and 537 for `getrlimit`, and
1592 because its command names a host on the internet (`UpstreamCaseScreening`).

- 96 (`curl --version`) and 558 (`lib558`) verify `%LOGDIR/memdump`, the allocation log upstream's
  debug build writes from its C allocator wrappers: lines such as `MEM tool_cfgable.c`, `MEM easy.c:
  malloc()` and `MEM dynbuf.c: realloc()`, each naming a C source file and allocator call.
- 506 needs a libcurl built without the threaded resolver. ADR-0695 already has Curl list
  `threaded-resolver` on every platform, as the reference curl builds (Schannel on Windows, OpenSSL on
  Linux and macOS) do, so upstream's own suite skips 506 for those builds too.
- 518 and 537 need `getrlimit`. `Lib518` and `Lib537` in `Curl.Conformance.LibTest.UnitLibrary` are the
  programs as they build without `getrlimit` and `setrlimit` (the base class library has neither): their
  `check` precheck prints `system lacks necessary system function(s)`, which skips the case as upstream's
  precheck does on such a build.
- 1592 (`lib1592`) adds a transfer to `http://a-site-never-accessed.example.org/1592`, runs one
  `curl_multi_perform` and checks that `curl_multi_remove_handle` returns at once while the name lookup is
  still unanswered. It needs a lookup that never answers, not the internet.

## Decision

1. **96 and 558 stay off.** Their memdump is a record of C `malloc`, `realloc` and `free` calls in named
   upstream source files. Curl has no such allocator; a stand-in could only write the expected lines,
   which would test nothing. `TrackMemory` stays off every `UpstreamCurlPlatform` (ADR-0711 reports it
   only to a ported unit program's case).
2. **506 stays off.** Curl lists `threaded-resolver` as the reference builds do (ADR-0695); reporting it
   absent for one case would claim a build Curl does not match.
3. **518 and 537 stay off.** `getrlimit` is not reported: the ports are the programs as built without it,
   and reaching the process descriptor limit from the BCL would need native calls in a test program. The
   precheck keeps skipping them as upstream skips them on such a build.
4. **1592 runs.** `UpstreamCaseScreening.StalledLookupHost` names `a-site-never-accessed.example.org`, which
   the internet-host screen no longer counts as the internet. The conformance tests' libtest engine resolves
   names through `StalledLookupDnsResolver`, whose lookup of that one name waits on the injected
   `TimeProvider` until it is cancelled (no `Thread.Sleep`), and every other name through
   `LoopbackOnlyDnsResolver`. 1592 passes on Windows and is on `PassingUpstreamCases.txt`.

## Consequences

- 96, 506, 518, 537 and 558 stay `Inconclusive` with their skip reasons; a gap finding naming them can
  close as `excluded` with this ADR as the reason.
- If Curl ever gains a debug allocation log, or a test program gains a descriptor-limit reader, revisit
  points 1 and 3 in a new ADR.
