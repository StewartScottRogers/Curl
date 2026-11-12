---
id: BL-4921
title: Generate one request per redirect chain for --csharp and --powershell with -L
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.CodeGeneration.UnitLibrary/CodeGenerationTransferHook.cs, Curl.Extension.CodeGeneration.UnitTests/CodeGenerationRedirectTests.cs]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-4921 — Generate one request per redirect chain for --csharp and --powershell with -L

## Goal

Code generated under `-L` makes exactly the requests the command line made: `GET /redir` then `GET /plain`.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding EXT-012 (Medium, confirmed: reproduced at least twice).

- Repro: `/redir` returns 302 to `/plain` (body `hello`). `curl -sS --csharp l.cs --powershell l.ps1 -L http://127.0.0.1:18800/redir`, then run the generated files.
- Expected: Help "sends this command line's requests"; ADR-1138 sets AllowAutoRedirect under `-L`: `hello` once.
- Actual: Each file holds a redirect-following `/redir` request plus a separate `/plain` request; server logs `/redir`, `/plain`, `/plain`; `hello` twice.
- Where to start: `CodeGenerationTransferHook.cs` records redirect follow-ups as new requests.

## Acceptance criteria

- [x] `l.ps1` contains one request, and running it logs exactly `GET /redir`, `GET /plain`.
- [x] `l.cs` likewise.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Fix: the hook skips a transfer whose `Http.RedirectsFollowed` is above 0 (a follow-up hop), so a chain gives one request. Both files use the same hook, so `l.cs` and `l.ps1` are both fixed; HttpClient follows the redirect itself under -L (ADR-1138). Pinned by CodeGenerationRedirectTests. CI platform criteria: change is platform-neutral.

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: f592c52c 6da66a29 a0b49a29.
- 2026-11-11: Doing -> Done. Redirect follow-up hops no longer add requests; one request per chain
