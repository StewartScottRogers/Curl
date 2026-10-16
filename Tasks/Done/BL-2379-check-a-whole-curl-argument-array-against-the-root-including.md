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
completed: 2026-10-15
---

## Goal

Add the command-line checker: given an argument array, return the first path that falls outside the root, naming the option, or an all-clear.

## Context

ADR-0548 (`Documentation/Planning/Decisions/ADR-0548-curl-mcp-an-mcp-server-over-the-api-seam-on-microsofts-sdk.md`) and the plan `Documentation/Planning/Curl-Mcp.md` decide the design: `Curl.Mcp` is the thin host holding the Microsoft MCP SDK wiring, `Curl.Mcp.UnitLibrary` holds all tool logic (base class library only), `Curl.Mcp.UnitTests` tests both. The two packages `ModelContextProtocol` 2.2.0 and `Microsoft.Extensions.Hosting` 10.0.x are approved for `Curl.Mcp` only.

Builds on the resolver and classification of the two previous tasks. It must find option values the way curl does (`-ofile`, `--output file`, combined short flags such as `-sSo file`, `--next` groups, `--` end of options, `-K` config files whose contents are themselves options and paths). Reuse `Curl.Cli.UnitLibrary`'s parser if it exposes the tokenised options without side effects; otherwise parse in this library. Embedded values: `-d @file`, `--data-binary @file`, `--data-urlencode name@file`, `--json @file` (`@-` is stdin, allowed), `-F name=@file;type=...`, `-F name=<file`, `-w @file`, `--variable name@file`, `--url-query @file`. URLs: `file://` (any host form) resolved as a path; an upload to a `file://` URL writes. `-K` is refused unless the config file is inside the root, and its contents are then checked recursively with a depth cap.

## Acceptance criteria

- [x] A checker type returns a result carrying the option name (as the user wrote it) and the offending path, or success; tests cover every classification kind from the previous task end to end through argument arrays, including combined short flags, `--next` groups, `--`, repeated options and options after the first violation.
- [x] Embedded forms: `@file` inside, `@file` outside, `@-` allowed, `name=@file`, `name=<file`, `;type=` suffix stripped, `--data-urlencode name@file`, `-w '@file'`, `%output{path}` inside a `-w` template (refused when outside).
- [x] `file:///inside` allowed, `file:///outside` refused, `file://localhost/x` handled, an upload to an outside `file://` URL refused; a URL given before `--url` or after `--next` is also checked.
- [x] A `-K` config inside the root whose content names `output = /outside` is refused; a config that includes itself does not loop (depth cap, tested); an unreadable config is refused.
- [x] The checker reads config files only when `-K` is present and never writes; tests use a temp directory.
- [x] - `dotnet build Curl.Mcp -warnaserror` and `dotnet build Curl.Mcp.UnitLibrary -warnaserror` are clean; `dotnet test Curl.Mcp.UnitTests --filter "TestCategory!=Integration"` is green; `powershell -NoProfile -File Measure-CodeQuality.ps1` reports no member of `Curl.Mcp.UnitLibrary` or `Curl.Mcp` outside the four gates (100% line, 100% branch, complexity at most 10, CRAP at most 30).

## Notes

Plan: Documentation/Planning/Curl-Mcp.md. Decision: ADR-0548.

Delivered (2026-10-15): `McpArgumentChecker` and `McpArgumentCheck` in `Curl.Mcp.UnitLibrary`, with the internal `McpOptionValuePaths` (embedded paths, `file:` URLs), `McpConfigFileSyntax` (`-K` line splitting) and `McpValuePath`; decisions in ADR-0560.

- `Curl.Cli`'s parser is not reused: it applies values, reading `-d @file` and `-K` files and prompting for passwords. The checker tokenises itself but reads `CommandLineOptionTable.Rows`, so `Curl.Mcp.UnitLibrary` now references `Curl.Cli.UnitLibrary` (replaces ADR-0559 decision 2's "the library does not"). `ConfigFileSyntax` is internal to `Curl.Cli`, outside `touches`, so its rules are ported without the warnings.
- Defaults taken: an empty value resolves to the root and is allowed (curl refuses it as blank anyway); `-O`/`-J` are allowed on the assumption that BL-2381 runs curl with the root as its working folder and without `.curlrc`; `-n`/`--netrc-optional`, `--abstract-unix-socket`, `--proto-default file`, `-K -`, globbed `file:` URLs and `-T` names, `-o` names with `#<n>`, and `--expand-` path values holding `{{` are refused because their path cannot be checked before curl runs.
- Left over: `%output{}` inside a `-w @file` template is not inspected (the checker reads only `-K` files) - filed as BL-2460.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Mcp.UnitLibrary`: 100% line, 100% branch, highest complexity 10, highest CRAP 10, no failing member. `Curl.Mcp.UnitTests`: 308 passed. `dotnet build Curl.Mcp -warnaserror` and `dotnet build` (solution) clean.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. McpArgumentChecker checks a whole curl argument array, embedded paths, file:// URLs and -K configs against CURL_MCP_ROOT
