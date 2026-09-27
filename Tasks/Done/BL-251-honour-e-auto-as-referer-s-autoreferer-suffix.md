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
completed: 2026-09-26
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

- [x] A test in `Curl.Cli.UnitTests` parses `-e "http://r.example/x;auto"` and asserts `Referer == "http://r.example/x"` and `AutoReferer == true`.
- [x] A test parses `-e ";auto"` and asserts `Referer` is `null` and `AutoReferer == true`.
- [x] A test parses `--referer ";auto"` (long form) with the same result as the short form.
- [x] Existing `-e` tests still pass unchanged: a plain `-e http://r.example/x` yields that `Referer` and `AutoReferer == false`; `-e ""` still yields an empty `Referer` and `AutoReferer == false`.
- [x] `CommandLineOptions.AutoReferer` has an XML doc comment saying it makes a followed redirect send the previous URL as `Referer`, per curl 8.21.0.
- [x] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean.
- [x] `dotnet test --filter "TestCategory!=Integration"` passes; no new test needs `TestCategory=Integration`.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and branch coverage for `Curl.Cli.UnitLibrary`, and no method above complexity 10 or CRAP 30.

## Notes

- Delivered directly rather than through the full `/feature` agent chain: the change is one
  setter and one property in `Curl.Cli.UnitLibrary`, fully specified by this task.
- `CommandLineOptionTable.SetReferer` matches `;auto` only as an ordinal, case-sensitive
  suffix (curl 8.21.0's `tool_getparam.c` compares the last five bytes). `;Auto` and
  `;auto/x` are kept verbatim with `AutoReferer` off; tests pin both.
- Each `-e` sets `AutoReferer` afresh, so a later `-e` without the suffix turns it off, as
  curl's `config->autoreferer = FALSE` branch does. Pinned by a test.
- `-e ""` keeps an empty `Referer` as the task's criteria require (curl frees it; both send
  no header, since an empty referer is not sent downstream).
- The old `Parse_Referer_KeepsTheValueAsGiven` rows for `;auto` asserted the verbatim value
  this task replaces; they are now `Parse_Referer_KeepsTheValueAsGivenWithoutAutoReferer`
  (plain, long form, empty, `;Auto`, `;auto/x`) plus the new `;auto` tests.
- Measure-CodeQuality: `Curl.Cli.UnitLibrary` 100/100, worst CRAP 10. The script exits
  non-zero for pre-existing gaps in `Curl.Networking` and `Curl.Protocol.File`, outside
  this task's `touches`.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. -e 'URL;auto' and -e ';auto' strip the suffix and set CommandLineOptions.AutoReferer
