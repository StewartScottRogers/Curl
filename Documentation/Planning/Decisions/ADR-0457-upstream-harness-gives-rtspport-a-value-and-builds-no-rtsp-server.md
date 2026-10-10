# ADR-0457 — The upstream harness gives %RTSPPORT a value and builds no RTSP server

- Status: Accepted
- Date: 2026-10-09
- Decided by Claude under Stewart's delegation (BL-1902)

## Context

Gap run 2026-10-08_2029 counted 9 upstream cases skipped because the case runner in
`Curl.Conformance.UnitLibrary` has no value for `%RTSPPORT`, and BL-1902 asked for a stand-in
of upstream's `rtspd` (`tests/server/rtspd.c`) so they would be measured.

Every one of the 9 (test567, 568, 569, 570, 571, 572, 577, 689 and 3100 at `curl-8_21_0`) is
a `<tool>` libtest: a C program (`lib567`, `lib568`, ...) linked against libcurl, not a curl
command line. No vendored case drives RTSP through the `curl` tool. The harness runs only
curl command lines, and Curl has no libcurl API for a libtest to call, so these cases would
be skipped for their `<tool>` even with an `rtspd` stand-in in place.

## Decision

1. The runner gives `%RTSPPORT` the value `8996` (`UpstreamCaseRunner.RtspPort`), so the 9
   cases are screened on their content and skip with their real reason, "the harness does not
   act on <client><tool>". `UpstreamCaseRunnerTests` pins that for all 9.
2. No `rtspd` stand-in is built, and `rtsp` stays out of the servers screening lets run: a
   server no case can reach would be untested against upstream and held to 100% coverage by
   synthetic tests alone. The connection to port 8996 reaches the `sws` emulation, as any
   port without a stand-in does.
3. When a vendored case drives RTSP through the `curl` tool (a later curl release, or the
   harness running libtests), the stand-in is built then, against that case.

## Consequences

- The gap report stops counting these 9 cases as a missing-variable gap; they join the
  libtest cases, whose cause is the `<tool>` part.
- BL-1902's first acceptance criterion (the 9 cases Passed or a real Curl difference) cannot
  be met by any server emulation; the task records this.
