---
id: BL-191
title: Parse the transfer-encoding and HTTP version options and refuse --http2 and --http3
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-152]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-191 — Parse the transfer-encoding and HTTP version options and refuse --http2 and --http3

## Goal

`--compressed`, `--raw`, `-0`/`--http1.0`, `--http1.1`, `--tr-encoding`, `--ignore-content-length`, `--path-as-is` and `--request-target` parse, and `--http2`, `--http2-prior-knowledge` and `--http3` are refused per the BL-152 ADR.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item C5. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-152 measured: `--http2` exits 2 with `curl: option --http2: the installed libcurl version does not support this` then `curl: try 'curl --help' or 'curl --manual' for more information`.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] `--http2`, `--http2-prior-knowledge` and `--http3` each exit 2 with the measured two lines.
- [x] Each accepted option sets its `CommandLineOptions` member; tests per option.
- [x] Every refusal and warning this option group can raise exits 2 with the exact lines measured on curl 8.21.0, and every `--no-` spelling curl accepts for these options is measured and tested (a test per spelling).
- [x] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

- Plan item: C5 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Delivered in-session rather than through separate architect/implementer agents: the change is one
  library and its tests, and the design follows the existing row pattern exactly. Conformance was
  checked by measuring every spelling below before pinning it.
- Measured on `/mingw64/bin/curl` 8.21.0 (Schannel), `curl <args> http://127.0.0.1:1/`, 2026-09-26:
  - `--http2`, `--http2-prior-knowledge`, `--http3`, `--http3-only`, `--http2=x`: exit 2,
    `curl: option <as typed>: the installed libcurl version does not support this` + try-help line.
    Same with `-s` before or after; `--http2 --bogus` refuses `--http2`; `-0 --http2` refuses without a warning.
  - `--no-http1.0` (and `=x`), `--no-http1.1`, `--no-http2`, `--no-http2-prior-knowledge`, `--no-http3`,
    `--no-http3-only`, `--no-request-target=x`: exit 2, `cannot be reversed with a --no- prefix`.
  - `--compressed`, `--raw`, `--tr-encoding`, `--ignore-content-length`, `--path-as-is`, their `--no-`
    spellings, `--compressed=x`, `--no-compressed=x`, `-0`, `--http1.0`, `--http1.1`: accepted (exit 7, connect).
  - `--request-target=`: `blank argument where content is expected`; `URL --request-target`: `requires parameter`.
  - `--http1.1 -0`: `Warning: Overrides previous HTTP version option`; `-0 --http1.1 -0` warns twice;
    `-0 -0`, `--http1.1 --http1.1`, `--http1.0 --http1.0=x` do not warn; `-s --http1.0 --http1.1` does not,
    `--http1.0 --http1.1 -s` does.
  - A `-K` file line `http2`: `curl: k.txt:1 config file option 'http2' the installed libcurl version does `,
    `curl: not support this`, `curl: option -K: the installed libcurl version does not support this`, try-help.
- Choices (sensible defaults, no new ADR needed; ADR-0017 already decides the refusal):
  - `--http3-only` is refused too, because ADR-0017 decision 2 names it and it was measured the same.
  - `CommandLineOptions.HttpVersion` is `HttpVersionPreference?` (reusing the Abstractions enum) so that
    "not given" is distinguishable from `--http1.1`; curl's override warning needs that.
  - New row kind `CommandLineOption.UnsupportedFlag` rather than special-casing in the parser, per
    `Curl.Cli.UnitLibrary/CLAUDE.md`.
- Follow-up filed: BL-262 (`--http0.9` / `--no-http0.9`, measured negatable, outside this goal).
  Wiring into `HttpRequestOptions` is already BL-236 and BL-245.
- Tests: `Curl.Cli.UnitTests/CommandLineTransferEncodingOptionTests.cs`; Cli suite 1096 passing.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. --compressed, --raw, --tr-encoding, --ignore-content-length, --path-as-is, --request-target, -0/--http1.0 and --http1.1 parse; --http2, --http2-prior-knowledge, --http3 and --http3-only are refused as curl 8.21.0 refuses them
