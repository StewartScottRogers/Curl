---
id: BL-5841
title: Fix fitness finding EXT-113: a .cmd/.bat plugin helper or mcp+stdio server gets the URL or its arguments split at =, , and ;
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Plugin.UnitLibrary/SystemHelperProcessStarter.cs, Curl.Extended.Protocol.McpClient.UnitLibrary/Stdio/McpServerProcessStarter.cs]
requirement: none
created: 2026-11-23
completed:
---
# BL-5841 — Fix fitness finding EXT-113: a .cmd/.bat plugin helper or mcp+stdio server gets the URL or its arguments split at =, , and ;

## Goal

EXT-113 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: a .cmd/.bat plugin helper or mcp+stdio server gets the URL or its arguments split at =, , and ;. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

This is an extension (Part B): it is judged against Curl's own documentation (`curl --extended-help`, `Documentation/Protocols.md`, `Documentation/Conformance/Extended/*.md`, the ADRs), never against C curl.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:

- **Severity:** Low (the helper silently gets a truncated argument; the `get <url>` line on stdin is still correct; no command injection)
- **Confidence:** Confirmed (2 runs per scheme family)
- **Protocol/area:** plugin (`curl-proto-<scheme>`), mcp+stdio, Windows
- **Repro:**
  1. A directory `plug` on PATH holding `curl-proto-zzq.cmd`, which logs `1=[%1] 2=[%2] 3=[%3]` to `args.log` and answers `ok` then `arg1=%1`, and `mcpsrv.cmd`, which logs its arguments the same way to `mcpargs.log`.
  2. `curl -sS -m 5 "zzq://h/x?a=b"` and `curl -sS -m 5 "zzq://h/a,b;c"`
  3. From PowerShell: `curl -sS -m 5 'mcp+stdio://localhost/mcpsrv.cmd?a=b'` and `curl -sS -m 5 'mcp+stdio://localhost/mcpsrv.cmd?a,b;c'`
- **Expected:** `Documentation/Conformance/Extended/Plugin.md` ADR1142-Invocation-1 (MUST): "The program runs with the URL as typed as its one argument"; Discovery-3: a `.cmd` helper gets the URL as its one argument. For mcp+stdio, each `&`-separated query item is one argument.
- **Actual (.NET):** plugin log `1=[zzq://h/x?a] 2=[b]` and `1=[zzq://h/a] 2=[b] 3=[c]`, stdout `arg1=zzq://h/x?a`, exit 0. mcp log `1=[a] 2=[b]` and `1=[a] 2=[b] 3=[c]`. (`& | < > ^ " %` are correctly refused with exit 7; the old Critical injection stays fixed.)
- **Likely location:** `Curl.Extended.Protocol.Plugin.UnitLibrary/SystemHelperProcessStarter.cs` and `Curl.Extended.Protocol.McpClient.UnitLibrary/Stdio/McpServerProcessStarter.cs`: `ProcessStartInfo.ArgumentList` quotes only arguments containing spaces or quotes, but cmd.exe also splits on `,` `;` `=`. For a batch target, always wrap each argument in double quotes.
- **Acceptance check:** `curl -sS -m 5 "zzq://h/x?a=b"` makes args.log show `1=["zzq://h/x?a=b"]` (or the URL unsplit) and `2=[]`.

## Acceptance criteria

- [ ] `curl -sS -m 5 "zzq://h/x?a=b"` makes args.log show `1=["zzq://h/x?a=b"]` (or the URL unsplit) and `2=[]`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.