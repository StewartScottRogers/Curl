---
id: BL-2379
title: Check a whole curl argument array against the root, including values that embed paths and file:// URLs
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2321, BL-2378]
touches: [Curl.Mcp.UnitLibrary, Curl.Mcp.UnitTests]
requirement: none
created: 2026-10-15
completed:
---

## Goal

Add the command-line checker: given an argument array, return the first path that falls outside the root, naming the option, or an all-clear.

## Context

ADR-0548 (`Documentation/Planning/Decisions/ADR-0548-curl-mcp-an-mcp-server-over-the-api-seam-on-microsofts-sdk.md`) and the plan `Documentation/Planning/Curl-Mcp.md` decide the design: `Curl.Mcp` is the thin host holding the Microsoft MCP SDK wiring, `Curl.Mcp.UnitLibrary` holds all tool logic (base class library only), `Curl.Mcp.UnitTests` tests both. The two packages `ModelContextProtocol` 2.2.0 and `Microsoft.Extensions.Hosting` 10.0.x are approved for `Curl.Mcp` only.

Builds on the resolver and classification of the two previous tasks. It must find option values the way curl does (`-ofile`, `--output file`, combined short flags such as `-sSo file`, `--next` groups, `--` end of options, `-K` config files whose contents are themselves options and paths). Reuse `Curl.Cli.UnitLibrary`'s parser if it exposes the tokenised options without side effects; otherwise parse in this library. Embedded values: `-d @file`, `--data-binary @file`, `--data-urlencode name@file`, `--json @file` (`@-` is stdin, allowed), `-F name=@file;type=...`, `-F name=<file`, `-w @file`, `--variable name@file`, `--url-query @file`. URLs: `file://` (any host form) resolved as a path; an upload to a `file://` URL writes. `-K` is refused unless the config file is inside the root, and its contents are then checked recursively with a depth cap.

## Acceptance criteria

- [ ] A checker type returns a result carrying the option name (as the user wrote it) and the offending path, or success; tests cover every classification kind from the previous task end to end through argument arrays, including combined short flags, `--next` groups, `--`, repeated options and options after the first violation.
- [ ] Embedded forms: `@file` inside, `@file` outside, `@-` allowed, `name=@file`, `name=<file`, `;type=` suffix stripped, `--data-urlencode name@file`, `-w '@file'`, `%output{path}` inside a `-w` template (refused when outside).
- [ ] `file:///inside` allowed, `file:///outside` refused, `file://localhost/x` handled, an upload to an outside `file://` URL refused; a URL given before `--url` or after `--next` is also checked.
- [ ] A `-K` config inside the root whose content names `output = /outside` is refused; a config that includes itself does not loop (depth cap, tested); an unreadable config is refused.
- [ ] The checker reads config files only when `-K` is present and never writes; tests use a temp directory.
- [ ] - `dotnet build Curl.Mcp -warnaserror` and `dotnet build Curl.Mcp.UnitLibrary -warnaserror` are clean; `dotnet test Curl.Mcp.UnitTests --filter "TestCategory!=Integration"` is green; `powershell -NoProfile -File Measure-CodeQuality.ps1` reports no member of `Curl.Mcp.UnitLibrary` or `Curl.Mcp` outside the four gates (100% line, 100% branch, complexity at most 10, CRAP at most 30).

## Notes

Plan: Documentation/Planning/Curl-Mcp.md. Decision: ADR-0548.

## Log

- 2026-10-15: Created.
