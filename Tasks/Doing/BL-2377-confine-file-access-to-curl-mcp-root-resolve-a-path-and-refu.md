---
id: BL-2377
title: Confine file access to CURL_MCP_ROOT: resolve a path and refuse one outside the root
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2321, BL-2376]
touches: [Curl.Mcp.UnitLibrary, Curl.Mcp.UnitTests]
requirement: none
created: 2026-10-15
completed:
---

## Goal

Add the path-confinement core to Curl.Mcp.UnitLibrary: given the root folder and a path, decide whether the path, fully resolved, lies inside the root.

## Context

ADR-0548 (`Documentation/Planning/Decisions/ADR-0548-curl-mcp-an-mcp-server-over-the-api-seam-on-microsofts-sdk.md`) and the plan `Documentation/Planning/Curl-Mcp.md` decide the design: `Curl.Mcp` is the thin host holding the Microsoft MCP SDK wiring, `Curl.Mcp.UnitLibrary` holds all tool logic (base class library only), `Curl.Mcp.UnitTests` tests both. The two packages `ModelContextProtocol` 2.2.0 and `Microsoft.Extensions.Hosting` 10.0.x are approved for `Curl.Mcp` only.

ADR-0548 decision 4: every path a tool would read or write must resolve inside one root, the `CURL_MCP_ROOT` environment variable. This task is only the resolver; the next tasks decide which curl options carry a path. Relative paths resolve against the root (the server runs curl with the root as its working directory). Base class library only: `Path.GetFullPath`, `FileSystemInfo.ResolveLinkTarget`. Compare with the platform's case rule (chosen by `OperatingSystem`, measured, not assumed) and with a trailing directory separator so `/root-evil` is not inside `/root`.

## Acceptance criteria

- [ ] A type `ConfinementRoot` (name may be refined to say what it does) is built from the root path, rejecting null, empty, relative, or non-existent roots with `ArgumentException` naming the cause, and reading the environment variable through an injected `Func<string,string?>` so tests set no real environment.
- [ ] Tests (platform-neutral, temp directory only) cover: a file inside, a nested file, the root itself, `..` escaping and `..` that stays inside, an absolute path outside, a sibling whose name starts with the root's name (`root` vs `root-evil`), a symlink inside that points outside (refused) and one that points inside (allowed; symlink tests report inconclusive only where the OS refuses to create one), a path whose parent is a symlink to outside, a not-yet-existing output file (judged by its nearest existing ancestor), and mixed separators.
- [ ] The result type says which path was refused and why (outside, unresolvable), so the caller can name the option; refusal of an ordinary outside path is a result, not an exception.
- [ ] A missing `CURL_MCP_ROOT` is an explicit result (no root configured), and every later check refuses file access in that state; a test pins it.
- [ ] - `dotnet build Curl.Mcp -warnaserror` and `dotnet build Curl.Mcp.UnitLibrary -warnaserror` are clean; `dotnet test Curl.Mcp.UnitTests --filter "TestCategory!=Integration"` is green; `powershell -NoProfile -File Measure-CodeQuality.ps1` reports no member of `Curl.Mcp.UnitLibrary` or `Curl.Mcp` outside the four gates (100% line, 100% branch, complexity at most 10, CRAP at most 30).

## Notes

Plan: Documentation/Planning/Curl-Mcp.md. Decision: ADR-0548.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
