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
completed:
---
# BL-346 — Parse --proxy-header and carry it into HttpRequestOptions.ProxyHeaders

## Goal

`--proxy-header` is accepted on the command line (repeatable, `@file` like `-H`) and its values reach `HttpRequestOptions.ProxyHeaders` in command-line order.

## Context

- Found by BL-296, which added `HttpRequestOptions.ProxyHeaders` and made the HTTP handler send it to a forward proxy after the `-H` values; nothing parses `--proxy-header` yet (`Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` has `header` but no `proxy-header`).
- Mirror `-H`: `CommandLineOptions.Headers` / `AddHeaders`, mapped in `Curl.Console/HttpRequestOptionsMapping.cs`.
- curl 8.21.0 reads `--proxy-header @file` one header per line like `-H @file`; measure before pinning.

## Acceptance criteria

- [ ] `--proxy-header "X-P: 1" --proxy-header "X-Q: 2"` yields `CommandLineOptions.ProxyHeaders` `["X-P: 1", "X-Q: 2"]`, covered in `Curl.Cli.UnitTests`, including the `@file` form.
- [ ] `HttpRequestOptionsMapping` copies them to `HttpRequestOptions.ProxyHeaders`, covered in `Curl.Console.UnitTests`.
- [ ] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-09-27: Created.
