---
id: BL-251
title: Honour -e ';auto' as --referer's autoreferer suffix
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-251 — Honour -e ';auto' as --referer's autoreferer suffix

## Goal

`curl -e 'URL;auto'` and `curl -e ';auto'` parse the way curl 8.21.0 does: the `;auto` suffix is stripped from `CommandLineOptions.Referer` and turns on a new `CommandLineOptions.AutoReferer` flag.

## Context

- Found while doing BL-172 (the HTTP request head formatter, `Curl.Protocol.Http.UnitLibrary/HttpRequestHeadFormatter.cs`).
- Measured on curl 8.21.0 (mingw, Windows) against a loopback recorder in BL-172:
  - `curl -e 'http://r.example/x;auto' http://127.0.0.1:18091/` sends `Referer: http://r.example/x`.
  - `curl -e ';auto' URL` sends no `Referer`.
  - `-e ''` (via config `referer = ""`) sends no `Referer`.
- Upstream: https://curl.se/docs/manpage.html#-e (curl 8.21.0): "Append ';auto' to the --referer URL to make curl automatically set the previous URL when it follows a Location: header. The ';auto' string can be used alone, even if you do not set an initial --referer."
- Today `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` line 81 stores the `-e` value verbatim into `CommandLineOptions.Referer` (`Curl.Cli.UnitLibrary/CommandLineOptions.cs` line 208), so `;auto` would be sent literally. `HttpRequestOptions.Referer` (`Curl.Protocol.Abstractions.UnitLibrary/HttpRequestOptions.cs`) is documented as the referring URL, so the CLI must strip the suffix.
- Scope: this task changes `Curl.Cli` only. It adds `public bool AutoReferer { get; internal set; }` (XML-documented) to `CommandLineOptions` beside `Referer`. No flag is added to `HttpRequestOptions`: the autoreferer is consumed by the redirect follower (BL-203, `Curl.Core`), which sets `Referer` to the previous URL on each followed redirect when the flag is on; the wiring of `AutoReferer` into that follower belongs to BL-203 / BL-234, not here.
- Match the suffix exactly as curl does: the value ends with `;auto` (case-sensitive). When it does, `Referer` becomes the text before it, or `null` if that text is empty (curl sends no header then). A value without the suffix keeps today's behaviour, including `-e ''` accepted as empty.
- Keep the setter a named method in `CommandLineOptionTable` (e.g. `SetReferer`) so the lambda does not grow past the CA1502 complexity limit.

## Acceptance criteria

- [ ] A test in `Curl.Cli.UnitTests` parses `-e "http://r.example/x;auto"` and asserts `Referer == "http://r.example/x"` and `AutoReferer == true`.
- [ ] A test parses `-e ";auto"` and asserts `Referer` is `null` and `AutoReferer == true`.
- [ ] A test parses `--referer ";auto"` (long form) with the same result as the short form.
- [ ] Existing `-e` tests still pass unchanged: a plain `-e http://r.example/x` yields that `Referer` and `AutoReferer == false`; `-e ""` still yields an empty `Referer` and `AutoReferer == false`.
- [ ] `CommandLineOptions.AutoReferer` has an XML doc comment saying it makes a followed redirect send the previous URL as `Referer`, per curl 8.21.0.
- [ ] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean.
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes; no new test needs `TestCategory=Integration`.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and branch coverage for `Curl.Cli.UnitLibrary`, and no method above complexity 10 or CRAP 30.

## Notes

## Log

- 2026-09-26: Created.
