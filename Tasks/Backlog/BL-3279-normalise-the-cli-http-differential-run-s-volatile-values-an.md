---
id: BL-3279
title: Normalise the CLI HTTP differential run's volatile values and name the reference build's remaining faults
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.Differential.UnitLibrary/DifferentialOutputNormaliser.cs, Curl.Conformance.Differential.UnitLibrary/ReferenceBuildLimitation.cs, Curl.Conformance.Differential.UnitLibrary/Cases/CliHttp, Curl.Conformance.Differential.UnitTests/DifferentialOutputNormaliserTests.cs, Curl.Conformance.Differential.UnitTests/Cases/CliHttp]
requirement: none
created: 2026-10-23
completed:
---
# BL-3279 — Normalise the CLI HTTP differential run's volatile values and name the reference build's remaining faults

## Goal

A CLI HTTP differential run (BL-3233) reports no difference that comes only from timing, ports, random keys or the reference build's own faults.

## Context

BL-3233's first full run (49 ### DIFF) shows these noise differences, all outside Curl's behaviour: elapsed milliseconds in "curl: (28) ... after N milliseconds" (max-time, max-time-fraction, max-time-body, connect-timeout-badhost, opt-retry-timeout); -w time_*, speed_download, local_port and the same fields inside %{json} (w-speed_download, w-time-vars-exist, w-json, w2-__local_port_, w2-__json_, w2-__time_total_, w2-fail-json, redir-write-out-multi); the PROXY line's source port (haproxy, haproxy-clientip); the random Sec-WebSocket-Key (ws-plain); cookie expiry seconds one apart (cookie-jar-then-send, cookie-expire-in-jar); If-Modified-Since from a file each run creates a second apart (time-cond-file); the debug build's "!!! WARNING" lines inside --trace-ascii output (output-trace-time, output-trace-ids); -Z output order (opt-parallel-stdout-interleave). Reference-build faults to name as ReferenceBuildLimitation values (ADR-0782 named exclusions): --dns-servers and --ech unsupported (dns-servers, ech); --test-event exists only in debug builds (test-event); the reference reads a password prompt from the console, not stdin, so it times out (basic-u-noPass-stdin, auth-basic-user-nocolon, auth-netrc-user-flag); its libpsl adds "PSL check set-cookie" verbose lines the platform's curl does not (cookie-write-v, redir-cookie-send, cookies2-jar-send, cookies2-mem); and its --version lists zlib, brotli, libidn2 and others (opt-version-list-features). Rerun CliHttpDifferentialTests with CURL_DIFFERENTIAL_REFERENCE set (see BL-3233 Notes).

## Acceptance criteria

- [ ] The normaliser masks each volatile value listed under Context, each with a unit test in Curl.Conformance.Differential.UnitTests.
- [ ] Each reference-build fault listed under Context is a named ReferenceBuildLimitation tagged on its cases.
- [ ] A rerun of CliHttpDifferentialTests reports none of the cases listed under Context as a ### DIFF.
- [ ] The fast tests pass and Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

## Log

- 2026-10-23: Created.
