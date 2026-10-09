---
id: BL-1852
title: Close GF-0042: --create-dirs does not create the --dump-header file's folder, so the transfer fails with exit 23 before any request
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1852 — Close GF-0042: --create-dirs does not create the --dump-header file's folder, so the transfer fails with exit 23 before any request

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0042 (--create-dirs does not create the --dump-header file's folder, so the transfer fails with exit 23 before any request), so a later gap analysis measures each of `behaviour:test3031` as `match`.

## Context

- Finding: GF-0042, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test3031`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Measured: test3031 expected 'upstream test3031 passes', actual '<verify><protocol> differs at byte 0 (line 1): expected "GET /this/is/the/3031 HTTP/1.1\r\n", got the end'. The command is --dump-header %PWD/%LOGDIR/tmp/out.txt --create-dirs. Reran on 6383c570 with the same result. Run directly, curl.exe -sS --dump-header <scratch>/tmp/out.txt --create-dirs http://127.0.0.1:1/x prints 'curl: Failed to open <scratch>/tmp/out.txt' and 'curl: (23) Failed writing received data to disk/application', and does not create tmp/. Reproduce from the repository root: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" Z:/repos/Curl.gap/2026-10-08_2029/scratch-gap-behaviour/raw.json 3031

Suggestion, copied from the finding:

In Curl.Console's CurlCommandRunner.TransferWithHeaderFileAsync, when options.CreateDirectories is set, create the -D file's missing parent folders before fileSystem.OpenForWriteAsync. Use the creator the -o path already uses for --create-dirs, with its errno-worded failure. Then the request is sent and the headers land in the new file, as upstream test3031 expects.

## Acceptance criteria

- [ ] `behaviour:test3031`: Curl answers what curl 8.21.0 answers, `upstream test3031 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
