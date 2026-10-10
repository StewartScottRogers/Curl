---
id: GF-0015
title: --retry does not follow a 429's Retry-After as curl does (resend, --fail, --retry-max-time warning)
area: behaviour
key: behaviour:retry-after-handling
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-08_1640
closed:
regression: false
items: [behaviour:test366, behaviour:test1633, behaviour:test1634]
touches: [Curl.Console, Curl.Console.UnitTests]
task: BL-1970
tasks: [BL-1808, BL-1970]
---
# GF-0015 - --retry does not follow a 429's Retry-After as curl does (resend, --fail, --retry-max-time warning)

## Summary

Curl differs from upstream curl in behaviour: --retry does not follow a 429's Retry-After as curl does (resend, --fail, --retry-max-time warning).

## Evidence

Every item expects 'upstream test<N> passes'. test1633 (-d moo --retry 1 -L): the --output file against <reply><data> differs at byte 177 (line 12): expected 'HTTP/1.1 301 OK', got the end. test1634 (--retry 1 --fail): expected 'HTTP/1.1 429 too many requests swsbounce', got 'HTTP/1.1 200 OK'. test366 (--retry 2 --retry-max-time 10 with a too-long Retry-After): stderr differs from the reference curl, which exits 0. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 366,1633,1634

## Suggestion

In Curl.Console's retry loop: on a 429 with Retry-After, keep the 429's output when the retry is made (test1634 under --fail). Resend the POST and then follow -L (test1633). When Retry-After exceeds --retry-max-time, give up the retry without a message, matching the reference curl's stderr for test366.

## Measurements

- 2026-10-08_1640: 3 of 3 items are gaps.
- 2026-10-08_2029: 3 of 3 items are gaps.
- 2026-10-10_0657: 3 of 3 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-behaviour.
- 2026-10-08_1731: Filed BL-1808.
- 2026-10-10_0756: Filed BL-1970.
