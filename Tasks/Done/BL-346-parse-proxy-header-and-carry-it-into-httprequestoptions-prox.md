---
id: BL-346
title: Parse --proxy-header and carry it into HttpRequestOptions.ProxyHeaders
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-296]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-346 — Parse --proxy-header and carry it into HttpRequestOptions.ProxyHeaders

## Goal

`--proxy-header` is accepted on the command line (repeatable, `@file` like `-H`) and its values reach `HttpRequestOptions.ProxyHeaders` in command-line order.

## Context

- Found by BL-296, which added `HttpRequestOptions.ProxyHeaders` and made the HTTP handler send it to a forward proxy after the `-H` values; nothing parses `--proxy-header` yet (`Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` has `header` but no `proxy-header`).
- Mirror `-H`: `CommandLineOptions.Headers` / `AddHeaders`, mapped in `Curl.Console/HttpRequestOptionsMapping.cs`.
- curl 8.21.0 reads `--proxy-header @file` one header per line like `-H @file`; measure before pinning.

## Acceptance criteria

- [x] `--proxy-header "X-P: 1" --proxy-header "X-Q: 2"` yields `CommandLineOptions.ProxyHeaders` `["X-P: 1", "X-Q: 2"]`, covered in `Curl.Cli.UnitTests`, including the `@file` form.
- [x] `HttpRequestOptionsMapping` copies them to `HttpRequestOptions.ProxyHeaders`, covered in `Curl.Console.UnitTests`.
- [x] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Delivered directly rather than through the full `/feature` agent chain: the change mirrors `-H` row for row, so there was no seam to plan (sensible default under unattended rule 1).
- Measured curl 8.21.0 (mingw, Schannel) on 2026-09-27: `--proxy-header bogus` and `--proxy-header ''` warn `Warning: The provided proxy header '<value>' does not look like a header?` (the word is `proxy`, not `HTTP`); `--proxy-header @nosuch` exits 26 with `curl: Failed to open nosuch` and `curl: option --proxy-header: error encountered when reading a file`; `--no-proxy-header` is not reversible; `-x http://127.0.0.1:18346 --proxy-header @ph.txt --proxy-header "X-Q: 2"` sends the file's non-empty lines then `X-Q: 2` to the proxy (a colonless line is dropped by the HTTP formatter, not the parser, as for `-H`).
- `-H` and `--proxy-header` now share one applier, `AddHeaderValue`, parameterised by the warning and the list to add to; `CommandLineWarning.ProxyHeaderDoesNotLookLikeAHeader` added. `HttpRequestOptionsMapping` copies `ProxyHeaders` verbatim.
- Tests: 9 new in `Curl.Cli.UnitTests/CommandLineHttpRequestOptionTests.cs`; `Create_HttpRequestOptions_AreMappedOntoHttp` in `Curl.Console.UnitTests` now covers `ProxyHeaders`. Build `-warnaserror` clean, fast tests all green, `dotnet format --verify-no-changes` clean on the touched files.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. --proxy-header is parsed (repeatable, @file, curl's proxy warning) and reaches HttpRequestOptions.ProxyHeaders in command-line order
