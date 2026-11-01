---
id: GF-0069
title: A config-file line over 10 MB is accepted; upstream rejects the file with 'cannot read config'
area: environment
platform: windows
key: environment:config-syntax-line-length-limit
severity: Medium
status: closed
scope: target
introduced-in:
opened: 2026-10-12_0951
closed: 2026-10-12_2042
regression: false
items: [environment:config-syntax:line-length-limit]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
task: BL-2115
tasks: [BL-2115]
---
# GF-0069 - A config-file line over 10 MB is accepted; upstream rejects the file with 'cannot read config'

## Summary

Curl differs from upstream curl in environment: A config-file line over 10 MB is accepted; upstream rejects the file with 'cannot read config'.

## Evidence

Recipe: a .curlrc with a comment line one byte over 10 MB, then 'write-out = gap-config-hit'. Expected: exit 0; stdout ''; stderr 'curl: cannot read config from ...' (the reference prints the path wrapped across lines). Actual: exit 0; stdout 'gap-config-hit'; stderr ''. A search of Curl.Cli.UnitLibrary/ConfigFileSyntax.cs finds no line-length limit. Reproduce: powershell -NoProfile -File Gap\Tools\Measure-EnvironmentGap.ps1 -OutFile $env:TEMP\environment.json

## Suggestion

Add the 10 MB (10485760-byte) per-line limit that docs/cmdline-opts/config.md states ('no more than 10 megabytes') to the config reader in Curl.Cli.UnitLibrary (ConfigFileSyntax / ConfigFileApplier). When a line exceeds it, stop reading the file and print 'curl: cannot read config from <path>' as the reference does. Measure the reference's exact stderr text and exit code first.

## Measurements

- 2026-10-12_0951: 1 of 1 items are gaps.
- 2026-10-12_2042: 0 of 1 items are gaps.
- 2026-10-13_1406: 0 of 1 items are gaps.
- 2026-10-14_0904: 0 of 1 items are gaps.
- 2026-10-15_0304: 0 of 1 items are gaps.
- 2026-10-15_0543: 0 of 1 items are gaps.
- 2026-10-15_0751: 0 of 1 items are gaps.
- 2026-10-15_1657: 0 of 1 items are gaps.
- 2026-10-17_2225: 0 of 1 items are gaps.
- 2026-10-19_0804: 0 of 1 items are gaps.
- 2026-10-19_1329: 0 of 1 items are gaps.
- 2026-10-22_0148: 0 of 1 items are gaps.
- 2026-10-23_1637: 0 of 1 items are gaps.
- 2026-10-25_1542: 0 of 1 items are gaps.
- 2026-10-30_2103: 0 of 1 items are gaps.
- 2026-10-31_1828: 0 of 1 items are gaps.

## Log

- 2026-10-12_0951: Opened by gap-environment.
- 2026-10-12_1039: Filed BL-2115.
- 2026-10-12_2042: Closed: run 2026-10-12_2042 measured every item as match or excluded.
