---
id: BL-056
title: Uppercase percent-escape hex digits in the file:// path curl quotes
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-015]
touches: [Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-056 — Uppercase percent-escape hex digits in the file:// path curl quotes

## Goal

`FileUrlPath.UrlPath` writes every `%xx` escape it keeps with uppercase hexadecimal
digits, so the exit 37 message quotes the path byte for byte as curl 8.21.0 does.

## Context

Measured during BL-015 on this machine against curl 8.21.0
(`curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel`), each exit 37:

- `file:///C:/dir/a%2eb/x` quotes `C:/dir/a%2Eb/x`.
- `file:///C:/dir/a%5cb` quotes `C:/dir/a%5Cb`.
- `file:///C:/dir%5c..%5cnosuch.txt` quotes `C:/dir%5C..%5Cnosuch.txt`.
- `file:///C:/dir/../a%20b%2fc` quotes `C:/a%20b%2Fc`.
- With `--path-as-is`, `file:///C:/dir/%2e%2e/x` quotes `C:/dir/%2E%2E/x`.

`Curl.Protocol.File.UnitLibrary\FileUrlPath.cs` keeps each escape exactly as written, so
`UrlPath` holds `%2e`, `%5c`, `%2f` in lowercase and the message differs from curl's.
`OsPath` is unaffected: decoding does not care about case.

Not yet measured, and to be measured before it is pinned: whether curl also rewrites a
malformed escape (`%2`, `%GG`, a trailing `%`), and whether it re-encodes characters
that were not escaped in the URL.

## Acceptance criteria

- [ ] A test in `FileUrlPathTests` asserts `UrlPath` is `C:/dir/a%2Eb/x` for
      `file:///C:/dir/a%2eb/x`, and `C:/a%20b%2Fc` for `file:///C:/dir/../a%20b%2fc`.
- [ ] A test asserts that with `pathAsIs: true`, `file:///C:/dir/%2e%2e/x` gives
      `UrlPath` `C:/dir/%2E%2E/x`.
- [ ] The malformed-escape cases are measured under curl 8.21.0, recorded in `Notes`,
      and pinned in a named test.
- [ ] `FileUrlPath`'s XML documentation for `UrlPath` states the uppercasing.
- [ ] `dotnet build Curl.Protocol.File.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Protocol.File.UnitTests --filter "TestCategory!=Integration"`
      is green.

## Notes

## Log

- 2026-09-26: Created.
