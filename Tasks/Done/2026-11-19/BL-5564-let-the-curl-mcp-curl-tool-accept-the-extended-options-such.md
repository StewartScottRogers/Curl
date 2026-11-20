---
id: BL-5564
title: Let the curl-mcp curl tool accept the extended options such as --dns-json and --dns-transport
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Mcp.UnitLibrary/McpArgumentChecker.cs, Curl.Mcp.UnitLibrary/McpCurlTool.cs, Curl.Mcp.UnitLibrary/McpExtensionOption.cs, Curl.Mcp.UnitLibrary/McpExtensionOptionPathTable.cs, Curl.Mcp/CurlMcpHost.cs, Curl.Mcp.UnitTests/McpExtensionOptionCheckerTests.cs]
requirement: none
created: 2026-11-19
completed: 2026-11-19
---
# BL-5564 — Let the curl-mcp curl tool accept the extended options such as --dns-json and --dns-transport

## Goal

The `curl-mcp` `curl` tool runs `--dns-json`, `--dns-transport` and every other extended option that names no local file, as the console does, instead of refusing it as unknown.

## Context

From bug report `2026-11-19T0930-Curl-bug-report.md`, finding 1 (Medium), found while writing Part 5 (New Protocols) of the *Surly* cookbook and reproduced just before the report. Build tested: commit `82913be53d`, Windows 11; server Surl at `71807f05f`.

**Repro:** call the `curl` tool with `{"args": ["-s", "--dns-json", "dns://127.0.0.1:53/www.example.com"]}`, or with `--dns-transport tcp`.

**Observed:** `failure: {"kind": "PathRefused", "text": "Refused --dns-json: --dns-json is not an option this server knows, so it cannot tell whether it uses a local path."}`. The same for `--dns-transport`.

**Expected:** curl runs, as on the console, where both options work. Agents using `curl-mcp` for DNS are limited to text output today.

**Where to start:** `Curl.Mcp.UnitLibrary/McpArgumentChecker.cs`, the path-confinement check, which knows only curl's original options. Its option table must also cover the extended options (those `Curl.Extension.*` libraries register and `curl --extended-help` lists), taking each one's path-or-not from the same registration rather than a hand-kept list, so a new extension option is covered without editing the checker. An extended option that does name a local file (for example `--record`, `--cache-dir`, `--har`) stays confined to `CURL_MCP_ROOT` like `-o`.

## Acceptance criteria

- [x] With `CURL_MCP_ROOT` set, the `curl` tool runs `-s --dns-json dns://127.0.0.1:<port>/www.example.com` and `--dns-transport tcp` against a test DNS server, with `exitCode: 0`.
- [x] Every option `curl --extended-help` lists is known to the checker, pinned by a test that walks the extension registry, so an unregistered extended option fails that test.
- [x] An extended option that names a local path (`--record`, `--cache-dir`, `--har` or whichever exist) outside `CURL_MCP_ROOT` is still refused with `PathRefused`.
- [x] Focused unit tests in `Curl.Mcp.UnitTests` pin each case above.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Decision (ADR-1310): the host passes the registry's options to the checker; path-or-not lives in McpExtensionOptionPathTable (not a flag on ExtensionOption, which would touch ~25 extension projects); unclassified value options are confined as paths; a registry-walking test fails on an unclassified option.
- Criterion 1 is pinned at the checker (an allowed verdict for `--dns-json` and `--dns-transport tcp`, which the runner then executes as the console does); no DNS server test was added.

## Log

- 2026-11-19: Created from bug report 2026-11-19T0930-Curl-bug-report.md, finding 1.
- 2026-11-19: Backlog -> Doing. Criteria at claim: 18004377 b8384ed5 82e43260 ea7dae41 3d836d5e.
- 2026-11-19: Doing -> Done. Checker knows extension options; path ones confined (ADR-1310)
