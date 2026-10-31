---
id: GF-0090
title: --ssl-sessions on Linux does not read the file and print the session-file warnings real curl prints
area: options
platform: linux
key: options:ssl-sessions-file-warnings
severity: Medium
status: closed
scope: target
introduced-in: 8.12.0
opened: 2026-10-15_0751
closed: 2026-10-19_1329
regression: false
items: [options:--ssl-sessions]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
task: BL-2717
tasks: [BL-2333, BL-2717]
---
# GF-0090 - --ssl-sessions on Linux does not read the file and print the session-file warnings real curl prints

## Summary

Curl differs from upstream curl in options on linux: --ssl-sessions on Linux does not read the file and print the session-file warnings real curl prints.

## Evidence

curl --ssl-sessions <tempfile> (placeholder file): expected 'exit 2: Warning: unrecognized line 1 in SSL session file | Warning: <file> | Warning: invalid shmax base64 encoding in line 2 | ... | curl: (2) no URL specified ...', actual 'exit 2: curl: (2) no URL specified | curl: try ...'. Reproduce on linux: curl --ssl-sessions <tempfile>. CommandLineOptionTable.cs line 268 stores SslSessionsFile with RefusedBySchannelBuild(), so on Linux the file is stored but never loaded or parsed.

## Suggestion

In Curl.Cli.UnitLibrary, where SslSessionsFile (CommandLineGlobalState, CommandLineOptions) is applied on non-Schannel builds, load the file when the option is parsed, before the no-URL check, and print curl's warnings to standard error: 'Warning: unrecognized line N in SSL session file', 'Warning: <path>' and 'Warning: invalid shmax base64 encoding in line N', following the session-file format in docs/cmdline-opts/ssl-sessions.md. The TLS library that holds session tickets also needs the load and save. The cause is OpenSSL-build-specific (Schannel refuses the option). Measure real curl for the exact warning text and line rules. Keep `--ai-help` right for the options this changes (CLAUDE.md).

## Measurements

- 2026-10-15_0751: 1 of 1 items are gaps.
- 2026-10-15_1657: 1 of 1 items are gaps.
- 2026-10-17_2225: 1 of 1 items are gaps.
- 2026-10-19_0804: 1 of 1 items are gaps.
- 2026-10-19_1329: 0 of 1 items are gaps.
- 2026-10-22_0148: 0 of 1 items are gaps.
- 2026-10-23_1637: 0 of 1 items are gaps.
- 2026-10-25_1542: 0 of 1 items are gaps.
- 2026-10-30_2103: 0 of 1 items are gaps.

## Log

- 2026-10-15_0751: Opened by gap-options.
- 2026-10-15_0900: Filed BL-2333.
- 2026-10-17_2318: Filed BL-2717.
- 2026-10-19_1329: Closed: run 2026-10-19_1329 measured every item as match or excluded on linux.
