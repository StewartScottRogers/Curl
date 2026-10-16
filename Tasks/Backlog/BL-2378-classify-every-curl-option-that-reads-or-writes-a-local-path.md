---
id: BL-2378
title: Classify every curl option that reads or writes a local path, from the option table
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2321, BL-2377]
touches: [Curl.Mcp.UnitLibrary, Curl.Mcp.UnitTests]
requirement: none
created: 2026-10-15
completed:
---

## Goal

List, in Curl.Mcp.UnitLibrary, every curl command-line option whose value is a local path (read or written), and how the value is used.

## Context

ADR-0548 (`Documentation/Planning/Decisions/ADR-0548-curl-mcp-an-mcp-server-over-the-api-seam-on-microsofts-sdk.md`) and the plan `Documentation/Planning/Curl-Mcp.md` decide the design: `Curl.Mcp` is the thin host holding the Microsoft MCP SDK wiring, `Curl.Mcp.UnitLibrary` holds all tool logic (base class library only), `Curl.Mcp.UnitTests` tests both. The two packages `ModelContextProtocol` 2.2.0 and `Microsoft.Extensions.Hosting` 10.0.x are approved for `Curl.Mcp` only.

The option table is `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` (and `CommandLineOptions.cs`); read it and upstream's manpage (https://curl.se/docs/manpage.html, for the curl version in `Gap/Baselines/target.json`). Candidates, not exhaustive: -o/--output, -O (named from the URL, written to the working directory), --output-dir, -D/--dump-header, -c/--cookie-jar, -b/--cookie (a value with no `=` is a file), --trace, --trace-ascii, --stderr, -T/--upload-file, -K/--config, --cacert, --capath, --cert, --key, --crlfile, --pinnedpubkey, the --proxy-* certificate options, --netrc-file, --hsts, --alt-svc, --etag-save, --etag-compare, --libcurl, --write-out `@file` and `%output{}`, --ssl-sessions, --unix-socket, SSH key and known-hosts files, --random-file, --egd-file, --variable name@file, the `@file` forms of -d/--data*/--json/--url-query, -F `@file` and `<file`, and `file://` URLs. Do not rely on this list: derive it from the table and prove completeness with the drift test below.

## Acceptance criteria

- [ ] A type in `Curl.Mcp.UnitLibrary` maps each option long name to a path use: `None`, `ReadsFile`, `WritesFile`, `WritesDirectory`, `ReadsOrWritesFile`, `ReadsFileOrLiteral` (-b), `ValueEmbedded` (the value may carry `@file`, `<file` or `name=@file`; handled by the next task), `UnixSocketPath`.
- [ ] A drift test enumerates every option in `CommandLineOptionTable` and fails, naming the option, when one has no classification, so a future option cannot slip past the confinement unclassified; the default for an unknown option is refusal.
- [ ] Each classification has a test citing the manpage sentence it rests on in a comment, and tests assert at least: -o write, -O write, --output-dir directory, -D write, -c write, -b file-or-literal, --trace write, --stderr write, -T read, -K read, --cacert/--capath/--cert/--key read, --netrc-file read, --hsts and --alt-svc read-write, --etag-save write, --etag-compare read, --libcurl write, -w `@file` read, -d `@` embedded.
- [ ] The curl version whose manpage was consulted is stated in the type's XML doc comment.
- [ ] - `dotnet build Curl.Mcp -warnaserror` and `dotnet build Curl.Mcp.UnitLibrary -warnaserror` are clean; `dotnet test Curl.Mcp.UnitTests --filter "TestCategory!=Integration"` is green; `powershell -NoProfile -File Measure-CodeQuality.ps1` reports no member of `Curl.Mcp.UnitLibrary` or `Curl.Mcp` outside the four gates (100% line, 100% branch, complexity at most 10, CRAP at most 30).

## Notes

Plan: Documentation/Planning/Curl-Mcp.md. Decision: ADR-0548.

## Log

- 2026-10-15: Created.
