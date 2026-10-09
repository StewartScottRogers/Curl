---
id: GF-0042
title: --create-dirs does not create the --dump-header file's folder, so the transfer fails with exit 23 before any request
area: behaviour
key: behaviour:dump-header-ignores-create-dirs
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-08_2029
closed:
regression: false
items: [behaviour:test3031]
touches: [Curl.Console, Curl.Console.UnitTests]
task:
tasks: []
---
# GF-0042 - --create-dirs does not create the --dump-header file's folder, so the transfer fails with exit 23 before any request

## Summary

Curl differs from upstream curl in behaviour: --create-dirs does not create the --dump-header file's folder, so the transfer fails with exit 23 before any request.

## Evidence

Measured: test3031 expected 'upstream test3031 passes', actual '<verify><protocol> differs at byte 0 (line 1): expected "GET /this/is/the/3031 HTTP/1.1\r\n", got the end'. The command is --dump-header %PWD/%LOGDIR/tmp/out.txt --create-dirs. Reran on 6383c570 with the same result. Run directly, curl.exe -sS --dump-header <scratch>/tmp/out.txt --create-dirs http://127.0.0.1:1/x prints 'curl: Failed to open <scratch>/tmp/out.txt' and 'curl: (23) Failed writing received data to disk/application', and does not create tmp/. Reproduce from the repository root: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" Z:/repos/Curl.gap/2026-10-08_2029/scratch-gap-behaviour/raw.json 3031

## Suggestion

In Curl.Console's CurlCommandRunner.TransferWithHeaderFileAsync, when options.CreateDirectories is set, create the -D file's missing parent folders before fileSystem.OpenForWriteAsync. Use the creator the -o path already uses for --create-dirs, with its errno-worded failure. Then the request is sent and the headers land in the new file, as upstream test3031 expects.

## Measurements

- 2026-10-08_2029: 1 of 1 items are gaps.

## Log

- 2026-10-08_2029: Opened by gap-behaviour.
