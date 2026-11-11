---
id: BL-4939
title: Stop dns --dns-json double-escaping TXT and CAA strings
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.DnsQuery.UnitLibrary/Wire/DnsRecordDataDecoder.cs, Curl.Extended.Protocol.DnsQuery.UnitLibrary/Rendering/DnsJsonRenderer.cs, Curl.Extended.Protocol.DnsQuery.UnitTests/DnsJsonTxtEscapingTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4939 — Stop dns --dns-json double-escaping TXT and CAA strings

## Goal

`--dns-json` writes TXT and CAA character-strings JSON-escaped once (ADR-1124).

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding EXT-017 (Low, confirmed: reproduced at least twice).

- Repro: DNS mock on 18709 whose TXT for example.com holds `hello` and `world "q" \`. `curl.exe -sS --dns-json "dns://127.0.0.1:18709/example.com?type=TXT"`
- Expected: `"txt":["hello","world \"q\" \\"]`.
- Actual: `"world \\\"q\\\" \\\\"` (zone-file escaping then JSON escaping). Text output is right.
- Where to start: `DnsRecordDataDecoder.cs` lines 55, 83, 90 (`DnsPresentation.CharacterString`) as rendered by `DnsJsonRenderer.cs`; keep raw strings for JSON.

## Acceptance criteria

- [ ] Parsing the repro's JSON gives `world "q" \` as the second TXT string (checked with `ConvertFrom-Json`).
- [ ] Text output is unchanged.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
