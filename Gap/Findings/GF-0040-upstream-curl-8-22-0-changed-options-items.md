---
id: GF-0040
title: Upstream curl 8.22.0 changed options items
area: options
platform: windows
key: options:newest
severity: High
status: closed
scope: target
introduced-in: 8.22.0
opened: 2026-10-09_0050
closed: 2026-10-15_0304
regression: true
items: [options:--httpsig-algo, options:--httpsig-headers, options:--httpsig-key, options:--httpsig-keyid, options:--httpsig-algo:argument, options:--httpsig-headers:argument, options:--httpsig-key:argument, options:--httpsig-keyid:argument]
touches: []
task: BL-2235
tasks: [BL-2171, BL-2235]
---
# GF-0040 - Upstream curl 8.22.0 changed options items

## Summary

Upstream curl 8.22.0 added, changed or removed options items since 8.21.0.

## Evidence

- options:--httpsig-algo: added in 8.22.0.
- options:--httpsig-headers: added in 8.22.0.
- options:--httpsig-key: added in 8.22.0.
- options:--httpsig-keyid: added in 8.22.0.

## Suggestion

Bring these items to Curl when the target moves to 8.22.0.

## Measurements

- 2026-10-08_2029: 0 of 4 items are gaps. 4 not in this run's measurement.
- 2026-10-10_0657: 0 of 4 items are gaps. 4 not in this run's measurement.
- 2026-10-11_0936: 0 of 4 items are gaps. 4 not in this run's measurement.
- 2026-10-11_1950: 0 of 4 items are gaps. 4 not in this run's measurement.
- 2026-10-12_0951: 0 of 4 items are gaps. 4 not in this run's measurement.
- 2026-10-12_2042: 0 of 4 items are gaps. 4 not in this run's measurement.
- 2026-10-13_1406: 0 of 4 items are gaps.
- 2026-10-14_0904: 8 of 8 items are gaps.
- 2026-10-15_0304: 0 of 8 items are gaps.
- 2026-10-15_0543: 0 of 8 items are gaps.
- 2026-10-15_0751: 0 of 8 items are gaps.
- 2026-10-15_1657: 0 of 8 items are gaps.
- 2026-10-17_2225: 0 of 8 items are gaps.
- 2026-10-19_0804: 0 of 8 items are gaps.
- 2026-10-19_1329: 0 of 8 items are gaps.
- 2026-10-22_0148: 0 of 8 items are gaps.
- 2026-10-23_1637: 0 of 8 items are gaps.
- 2026-10-25_1542: 0 of 8 items are gaps.
- 2026-10-30_2103: 0 of 8 items are gaps.

## Log

- 2026-10-09_0050: Opened from the release diff 8.21.0 to 8.22.0.
- 2026-10-13: Scope newest -> target: ADR-0479 moves the targeted version to 8.22.0 (BL-2128).
- 2026-10-13_1406: Closed: run 2026-10-13_1406 measured every item as match or excluded.
- 2026-10-13: Filed BL-2171 by hand: the Windows reference is built without the experimental --httpsig-* options, so this run measured them as match and closed the finding, but Curl implements them as an intended difference (CLAUDE.md "Decisions", ADR-0454 precedent; BL-2128).
- 2026-10-14_0904: Added options:--httpsig-algo:argument, options:--httpsig-headers:argument, options:--httpsig-key:argument, options:--httpsig-keyid:argument from gap-options.
- 2026-10-14_0904: Reopened as a regression: run 2026-10-14_0904 measured options:--httpsig-algo, options:--httpsig-headers, options:--httpsig-key, options:--httpsig-keyid, options:--httpsig-algo:argument, options:--httpsig-headers:argument, options:--httpsig-key:argument, options:--httpsig-keyid:argument as gap again.
- 2026-10-14_0950: Filed BL-2235.
- 2026-10-15_0304: Closed: run 2026-10-15_0304 measured every item as match or excluded.
