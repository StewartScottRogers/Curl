---
id: GF-0085
title: The stderr of a -OJ --output-dir download whose directory is a regular file differs from curl's on Linux
area: behaviour
platform: linux
key: behaviour:output-open-warning-unix-strerror
severity: Medium
status: closed
scope: target
introduced-in:
opened: 2026-10-15_0751
closed: 2026-10-15_1657
regression: false
items: [behaviour:test3036]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Abstractions.UnitLibrary]
task: BL-2328
tasks: [BL-2328]
---
# GF-0085 - The stderr of a -OJ --output-dir download whose directory is a regular file differs from curl's on Linux

## Summary

Curl differs from upstream curl in behaviour on linux: The stderr of a -OJ --output-dir download whose directory is a regular file differs from curl's on Linux.

## Evidence

behaviour:test3036 (http://127.0.0.1:<port>/ --no-clobber --output-dir %LOGDIR/present -OJ --retry 1 --retry-all-errors, where %LOGDIR/present is a file): expected 'reference curl exits 23; stdout 0 bytes', actual 'stderr differs'. The exit code and request bytes agree (the cross-check compares them first). Same on macOS. Matches on Windows. Reproduce on linux: pwsh -File Gap/Tools/Measure-ReferenceCrossCheck.ps1 -Measurement <run>/measurements/linux/behaviour.json -TestsData <release>/tests/data -OutFile $TMPDIR/behaviour.json

## Suggestion

Make Curl.Console's 'Warning: Failed to open the file <path>: <reason>' line (OutputFileOpenWarning.ReasonFor) give the platform's strerror text. Today it hard-codes the Windows C runtime's texts, measured with Schannel curl. Opening <file>/MMM3036MMM fails on Linux and macOS with ENOTDIR, and curl prints 'Not a directory', while Windows gives 'No such file or directory'. Map the failure to Unix's text off Windows. Curl.Engine.UnitLibrary CRuntimeErrorNumbers and Curl.Cli.UnitLibrary DiskDataFileReader already hold 'Not a directory'. This needs a FileAccessStatus value (Curl.Protocol.Abstractions.UnitLibrary) for a non-directory path component, so check any other Unix-only wording in the retry and warning lines too. This is the most likely cause; the cross-check does not store the two binaries' stderr. Pin each platform's text in its own test in Curl.Console.UnitTests, the Unix one under [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)].

## Measurements

- 2026-10-15_0751: 1 of 1 items are gaps.
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

- 2026-10-15_0751: Opened by gap-behaviour.
- 2026-10-15_0900: Filed BL-2328.
- 2026-10-15_1657: Closed: run 2026-10-15_1657 measured every item as match or excluded on linux.
