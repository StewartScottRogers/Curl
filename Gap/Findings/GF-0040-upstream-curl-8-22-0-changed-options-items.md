---
id: GF-0040
title: Upstream curl 8.22.0 changed options items
area: options
key: options:newest
severity: High
status: open
scope: newest
introduced-in: 8.22.0
opened: 2026-10-09_0050
closed:
regression: false
items: [options:--httpsig-algo, options:--httpsig-headers, options:--httpsig-key, options:--httpsig-keyid]
touches: []
task:
tasks: []
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

## Log

- 2026-10-09_0050: Opened from the release diff 8.21.0 to 8.22.0.
