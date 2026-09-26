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
completed: 2026-09-26
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

- [x] A test in `FileUrlPathTests` asserts `UrlPath` is `C:/dir/a%2Eb/x` for
      `file:///C:/dir/a%2eb/x`, and `C:/a%20b%2Fc` for `file:///C:/dir/../a%20b%2fc`.
- [x] A test asserts that with `pathAsIs: true`, `file:///C:/dir/%2e%2e/x` gives
      `UrlPath` `C:/dir/%2E%2E/x`.
- [x] The malformed-escape cases are measured under curl 8.21.0, recorded in `Notes`,
      and pinned in a named test.
- [x] `FileUrlPath`'s XML documentation for `UrlPath` states the uppercasing.
- [x] `dotnet build Curl.Protocol.File.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Protocol.File.UnitTests --filter "TestCategory!=Integration"`
      is green.

## Notes

Measured on this machine against curl 8.21.0 (`x86_64-w64-mingw32`, Schannel), each exit 37:

- Well-formed escapes are uppercased whatever they encode: `a%e9b` quotes `a%E9b`,
  `%7e` quotes `%7E` (not decoded to `~`), `a%ffb` quotes `a%FFb`, and
  `--path-as-is` on `%2e%2e` quotes `%2E%2E`.
- Malformed escapes are quoted exactly as written, case untouched: `a%2/x`, `a%GG/x`,
  `a%g2b`, `a%2gb`, a trailing `a%` and `a%2`. In `a%%2eb` the first `%` is malformed
  and the `%2e` after it is not: curl quotes `a%%2Eb`.
- Re-encoding of unescaped characters: printable ASCII is kept (`a"b` quotes `a"b`), a
  literal space is exit 3, and `é` typed in Git Bash quotes `%E9` (code page 1252, not
  UTF-8). That is new behaviour, not this task's: filed as BL-112.

Choice: the uppercasing is applied to `UrlPath` only, as the last step before the record
is built, by a new `UppercaseEscapes` that reuses `TryReadEscape` - so "well-formed"
means exactly what the decoder already means by it. `OsPath` is still decoded from the
text before uppercasing; case does not change what it decodes to.

Existing test `TryParse_EscapedBackslash_IsNotASeparatorForDotSegmentRemoval` now
expects `C:/dir%5C..%5Cx`, matching curl's measured `C:/dir%5C..%5Cnosuch.txt`.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. UrlPath quotes every well-formed %xx escape with uppercase hex digits and malformed ones as written, matching curl 8.21.0's exit 37 message
