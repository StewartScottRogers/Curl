---
id: BL-5408
title: Fail with exit 8 or 56 when a curl-proto helper exits before answering capabilities
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Plugin.UnitLibrary/HelperExchange.cs, Curl.Extended.Protocol.Plugin.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-5408 — Fail with exit 8 or 56 when a curl-proto helper exits before answering capabilities

## Goal

A `curl-proto-<scheme>` helper that exits (or closes its output) before finishing its `capabilities` list ends the transfer with exit 8 or 56 and a message naming the early end, instead of looking like a helper that lacks the get/put capability (exit 1).

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding EXT-081 (Low, Confirmed: get and put, 2 runs each). Area: plugin helpers, Windows.

**Repro.** A directory `plug2` holding `curl-proto-bar.cmd` with CRLF lines `@echo helper broke 1>&2` and `@exit /b 3`. Put `plug2` on PATH.
`curl -sS -m 10 bar://host/x`
`curl -sS -m 10 -d z bar://host/x`

**Expected.** `Plugin.md` (ADR-1142): "Any other reply, or the output ending first, is exit 8"; a helper "exiting non-zero" is exit 56 with `<helper> exited with status <n>`.
**Actual (PORT).** `curl: (1) curl-proto-bar.cmd does not offer the get capability` (or `put`), exit 1, so a crashed helper looks like a working helper that lacks the feature.
**Where to start.** `Curl.Extended.Protocol.Plugin.UnitLibrary/HelperExchange.cs`, `ReadCapabilitiesAsync`: the loop `while (await ReadLineAsync() is { Length: > 0 } line)` treats end of output (null) like the terminating empty line. On null, wait for the exit status and fail with 8 (`Weird(...)`, as `ReadReplyAsync` does for "closed its output without a reply"), or with 56 and `exited with status <n>` when the helper exited non-zero.

## Acceptance criteria

- [ ] New tests in `Curl.Extended.Protocol.Plugin.UnitTests` (a new file, using the scripted helper process) prove a helper whose output ends before the empty line that ends `capabilities` fails with exit 8 (or 56 with `exited with status 3` when its exit status is 3), for both a get and a put transfer.
- [ ] A test proves a helper that sends its capabilities and the terminating empty line, listing only `put`, still gives `does not offer the get capability` with exit 1 for a get transfer.
- [ ] A test proves a helper that lists `get` and the empty line still transfers (no regression), and a mandatory unknown capability (`*x`) is still refused as before.
- [ ] Tests are platform-neutral.
- [ ] Hand check recorded in the Log: the first command exits 8 or 56 with a message naming the early end of output or `exited with status 3`.
- [ ] `Plugin.md` states which of 8 and 56 applies to an early end; `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: the first command exits 8 or 56 with a message naming the early end of output or `exited with status 3`; a helper whose capabilities list only `put` still gives `does not offer the get capability`, exit 1.

## Log

- 2026-11-17: Created.
