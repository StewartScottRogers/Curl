---
id: BL-4929
title: Send a domain-less cookie from a -b file only to IP-address hosts
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cookies.UnitLibrary/Cookie.cs, Curl.Cookies.UnitLibrary/CookieStore.cs, Curl.Cookies.UnitTests/DomainlessCookieMatchTests.cs]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-4929 — Send a domain-less cookie from a -b file only to IP-address hosts

## Goal

A header-style `Set-Cookie:` line with no domain in a `-b` file matches only IP-literal hosts, as curl does.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding STD-019 (Low, confirmed: reproduced at least twice).

- Repro: Cookie-echo server on 18200. File `in3.txt` = `Set-Cookie: hdrstyle=v11; path=/`. `curl -s -b in3.txt http://localhost:18200/show`; with `--resolve www.example.test:18200:127.0.0.1 http://www.example.test:18200/show`; `curl -s -b in3.txt http://127.0.0.1:18200/show`.
- Expected: REF 8.22.0: `none`, `none`, `hdrstyle=v11`. Same for lower-case `set-cookie:` and mixed files.
- Actual: `hdrstyle=v11` for all three.
- Where to start: `Cookie.cs` (doc says a null Domain "is sent to every host") and matching in `CookieStore.cs`.

## Acceptance criteria

- [x] The localhost and www.example.test commands print `none`; the 127.0.0.1 command prints `hdrstyle=v11`.
- [x] The `Cookie` doc comment states the rule.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Decision: a null Domain now matches only IP-literal hosts (IsIpAddressLiteral, the rule already used for IP hosts); existing 127.0.0.1 tests unchanged. Quality run for Curl.Cookies.UnitLibrary: 0 failing members. CI on Linux/macOS: change is platform-neutral.

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 30a5e699 8c392f32 a0b49a29.
- 2026-11-11: Doing -> Done. Domain-less cookies now match only IP hosts; tests and quality run green
