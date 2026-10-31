---
id: GF-0068
title: NETRC variable is not read; --netrc fails with exit 26 instead of using the named file
area: environment
platform: windows
key: environment:netrc-variable-ignored
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-12_0951
closed: 2026-10-12_2042
regression: false
items: [environment:NETRC]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
task: BL-2114
tasks: [BL-2114]
---
# GF-0068 - NETRC variable is not read; --netrc fails with exit 26 instead of using the named file

## Summary

Curl differs from upstream curl in environment: NETRC variable is not read; --netrc fails with exit 26 instead of using the named file.

## Evidence

Recipe: NETRC=<folder>/gap-netrc (file holds 'machine 127.0.0.1 login gapuser password gappass'), 'curl -s --netrc http://127.0.0.1:<port>/'. Expected: exit 0; stdout ''; stderr ''; request GET / HTTP/1.1. Actual: exit 26; stdout ''; stderr ''; request none. A search of Curl.Cli.UnitLibrary/NetrcUse.cs finds no NETRC variable lookup. Reproduce: powershell -NoProfile -File Gap\Tools\Measure-EnvironmentGap.ps1 -OutFile $env:TEMP\environment.json

## Suggestion

Make the netrc file lookup behind --netrc (Curl.Cli.UnitLibrary NetrcUse, and the code that opens the file) read the NETRC environment variable and use the named file, as docs/cmdline-opts/_ENVIRONMENT.md and docs/libcurl/libcurl-env.md (NETRC) describe. Fall back to the home-directory .netrc (_netrc on Windows) only when NETRC is unset. Add unit tests with an injected environment.

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

## Log

- 2026-10-12_0951: Opened by gap-environment.
- 2026-10-12_1039: Filed BL-2114.
- 2026-10-12_2042: Closed: run 2026-10-12_2042 measured every item as match or excluded.
