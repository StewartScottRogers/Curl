---
id: GF-0011
title: The Unix socket path is read differently
area: options
platform: linux
key: options:the-unix-socket-path-is-read-differently
severity: Medium
status: open
scope: target
introduced-in: 7.40.0
opened: 2026-10-15_1200
closed:
regression: false
items: [options:--unix-socket]
touches: [Curl.Console]
task:
tasks: []
---
# GF-0011 - The Unix socket path is read differently

## Summary

Fixture finding for New-TasksFromGaps.ps1's self-test: a linux finding.

## Evidence

Evidence text of GF-0011: `curl --unix-socket /tmp/s http://localhost/` exits 7 on the reference and 2 on Curl.

## Suggestion

Suggestion text of GF-0011: read the path as curl does on Linux.

## Measurements

- 2026-10-15_1200: 1 of 1 items are gaps.

## Log

- 2026-10-15_1200: Opened by gap-options.
