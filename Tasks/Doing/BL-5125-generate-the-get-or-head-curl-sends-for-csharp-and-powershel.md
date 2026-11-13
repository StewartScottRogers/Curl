---
id: BL-5125
title: Generate the GET (or HEAD) curl sends for --csharp and --powershell with -G instead of a POST with a body (EXT-052)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.CodeGeneration.UnitLibrary/GeneratedRequest.cs, Curl.Extension.CodeGeneration.UnitTests/GeneratedRequestGetModeTests.cs, Documentation/Planning/Decisions/ADR-1138-code-generation-extension-writes-httpclient-csharp-and-invoke-restmethod-powershell.md]
requirement: none
created: 2026-11-13
completed:
---
# BL-5125 — Generate the GET (or HEAD) curl sends for --csharp and --powershell with -G instead of a POST with a body (EXT-052)

## Goal

`--csharp` and `--powershell` with `-G` generate the request curl actually sends (a GET with the data in the URL and no body, or HEAD with `-I -G`) instead of a POST with a body.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding EXT-052 (Medium, Confirmed). The generated program performs a different operation from the command it was generated from.

**Repro.**
1. `curl -sS -m 5 --csharp g4.cs -G -d a=1 http://127.0.0.1:28000/echo -o NUL`, then look at the lines around `HttpMethod` in `g4.cs`.
2. `curl -sS -m 5 --powershell g4.ps1 -G -d a=1 http://127.0.0.1:28000/echo -o NUL`, then look at the `Invoke-RestMethod` line in `g4.ps1`.
3. Also with `-G --data-urlencode "q=a b"` and with `-I -G -d a=1`.

**Expected.** ADR-1138 "What is sent": the generated code uses "the method curl sends ... the URL as sent". The server log shows curl sent `GET /echo?a=1` with no body (HEAD with `-I -G`).

**Actual.** `g4.cs` contains `new HttpMethod("POST"), "http://127.0.0.1:28000/echo?a=1"` plus `request.Content = ... GetBytes("a=1")` and a form Content-Type; `g4.ps1` contains `-Method Post ... -Body ([System.Text.Encoding]::UTF8.GetBytes('a=1'))`. Both exit 0 with no warning.

**Where to start.** `Curl.Extension.CodeGeneration.UnitLibrary/GeneratedRequest.cs`: `DefaultMethod` (about line 100) returns POST when `http.Body` or `context.PostData` is set, and `BodyOf` (line 75) reads `http.Body` / `context.PostData`; neither accounts for `-G`. With `-G` the command-line layer (`Curl.Cli.UnitLibrary`, see `QueryUrl.cs` and `SelectedHttpMethod.cs`) appends the data to the URL query, so the URL in the context already holds `?a=1`; find how the context still carries the data and whether it carries a `-G` flag. If the extension cannot tell (no flag or the data is still in `PostData`), the smallest honest fix may be exposing a read-only "data became the query" indicator on the context or `HttpRequestOptions` (`Curl.Protocol.Abstractions.UnitLibrary`), which would widen `touches`: add the files you change to `touches` and keep that contract change minimal. Also check `Curl.Extension.RecordReplay.UnitLibrary/ExchangeKey.cs`, which has the same blind spot (a separate Low finding, `%{method}` POST for a replayed `-G -d`); do not fix it here, but if the new indicator makes it a one-line fix, say so in the Log for a follow-up task.

## Acceptance criteria

- [ ] New tests in `Curl.Extension.CodeGeneration.UnitTests/GeneratedRequestGetModeTests.cs` (new) prove that for `-G -d a=1` the generated C# holds `new HttpMethod("GET"), "http://127.0.0.1:28000/echo?a=1"` with no `request.Content` and no form Content-Type, and the generated PowerShell holds `-Method Get` with no `-Body`.
- [ ] A test proves `-G --data-urlencode "q=a b"` generates a GET with the encoded query in the URL and no body, in both languages.
- [ ] A test proves `-I -G -d a=1` generates HEAD (C# `HttpMethod.Head` or `new HttpMethod("HEAD")`, PowerShell `-Method Head`) with no body.
- [ ] Regression tests prove `-d a=1` without `-G` still generates a POST with the body, as before.
- [ ] ADR-1138 "What is sent" states the `-G` rule.
- [ ] The tests are platform-neutral (no file path or newline assumption beyond what the existing tests make).
- [ ] `dotnet build Curl.Extension.CodeGeneration.UnitLibrary -warnaserror` is clean, `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Review acceptance check, for a hand check: step 1 generates `new HttpMethod("GET"), "http://127.0.0.1:28000/echo?a=1"` with no `request.Content`; step 2 generates `-Method Get` with no `-Body`; `-I -G -d a=1` generates HEAD.
- Code generation is tested against ADR-1138, not against curl; the server in the repro can be a PowerShell or C# file-based app (never Python).

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: 5965c8aa bd04f8d6 93ebc639 bc985fba 6349d50e 9160ec01 b6e3d40a.
