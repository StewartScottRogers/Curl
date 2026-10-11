---
id: BL-2088
title: Print curl's warning for --anyauth or --proxy-anyauth with upload from stdin (upstream test1072)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-11
completed:
---
# BL-2088 — Print curl's warning for --anyauth or --proxy-anyauth with upload from stdin (upstream test1072)

## Goal

Curl prints curl's warning when `--anyauth` or `--proxy-anyauth` is combined with an upload from stdin, as curl 8.21.0 does, so the stderr of upstream test1072 matches.

## Context

curl's tool prints `Warning: Using --anyauth or --proxy-anyauth with upload from stdin involves a big risk of it not working. Use a temporary file or a fixed auth type instead!` when the upload source is stdin (`-T -`) and the auth mode is any-auth; Curl never prints it. Verify the exact text, the line wrapping and the exact conditions (does `-d @-` count?) from curl 8.21.0's src/tool_operate.c or by measuring real curl with `Record-CurlExchange.ps1` before pinning. Warnings live in Curl.Console (see WarningLineWrapper.cs, OutputFileOpenWarning.cs and RemoteTimeFailureWarning.cs for the pattern; the `--anyauth` mapping is in HttpRequestOptionsMapping.cs). Upstream test1072 uses `-T - -u testuser:testpass --anyauth` against a 401 HTTP/1.0 server and expects exit 25.

Source: re-measurement of the gap findings with the fixed gap harness (PR #119, 2026-10-11); parent re-close task BL-2075. Targeted curl version: 8.21.0 (tests/data in the release tarball).

## Acceptance criteria

- [ ] New MSTests in Curl.Console.UnitTests (no network, platform-neutral) pin the warning bytes on stderr for `-T - --anyauth` and `-T - --proxy-anyauth`, and show no warning for `-T file --anyauth` or `-T - --basic`.
- [ ] `--ai-help` is unaffected (no option is added or changed); the task Log says so.
- [ ] Add test1072 once BL-2087 and the HTTP/1.0 retry (BL-2086) also match to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` once it passes in process.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.

## Notes

## Log

- 2026-10-11: Created.
