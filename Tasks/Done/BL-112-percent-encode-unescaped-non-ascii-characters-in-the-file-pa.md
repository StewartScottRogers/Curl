---
id: BL-112
title: Percent-encode unescaped non-ASCII characters in the file:// path curl quotes
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-056]
touches: [Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-112 — Percent-encode unescaped non-ASCII characters in the file:// path curl quotes

## Goal

`FileUrlPath.UrlPath` percent-encodes a non-ASCII character written unescaped in a `file://` URL, so the exit 37 message quotes it as curl 8.21.0 does.

## Context

Measured during BL-056 on this machine against curl 8.21.0 (`curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel`), each exit 37:

- `file:///C:/dir/é` (typed in Git Bash) quotes `C:/dir/%E9` - a single byte, so the
  argument reached curl in the Windows ANSI code page (1252), not as UTF-8 `%C3%A9`.
- `file:///C:/dir/a"b` quotes `C:/dir/a"b`: printable ASCII is not re-encoded.
- `file:///C:/dir/a b` (a literal space) is exit 3, `URL rejected: Malformed input to a URL function`.

`Curl.Protocol.File.UnitLibrary\FileUrlPath.cs` keeps unescaped characters as written. Before pinning,
measure which encoding curl uses for a character outside code page 1252 and when the
argument arrives through `CommandLineToArgvW` as UTF-16, since .NET receives `args` as UTF-16.

## Acceptance criteria

- [x] The encoding curl 8.21.0 applies to an unescaped non-ASCII character is measured for a
      code page 1252 character and one outside it, and recorded in `Notes`.
- [x] A named test in `FileUrlPathTests` pins `UrlPath` for each measured case.
- [x] `dotnet test Curl.Protocol.File.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

- Measured 2026-09-26 on this machine (ANSI code page 1252) with `file:///C:/nodir/a<X>b`, every case exit 37:

  | Character | Unicode build (`C:\Windows\System32\curl.exe`, 8.21.0 Schannel WinIDN) | ANSI mingw build (Git's `curl.exe`, 8.21.0) |
  | --- | --- | --- |
  | U+00E9 `é` (in 1252) | `a%C3%A9b` | `a%E9b` |
  | U+20AC `€` (1252 byte 0x80) | `a%E2%82%ACb` | `a%80b` |
  | U+03A9 `Ω` (outside 1252) | `a%CE%A9b` | `aOb` (best-fit) |
  | U+65E5 `日` (outside 1252) | `a%E6%97%A5b` | `a` (became `?`, cut as a query) |
  | U+1F600 emoji | `a%F0%9F%98%80b` | `a` |

  Also `file:///C:/dir/../a"%e9éb` quotes `C:/a"%E9%C3%A9b` on the Unicode build.
- Choice: pin the Unicode build. It receives its arguments as UTF-16 through `wmain`, exactly as .NET receives `args`; the mingw build's code-page bytes and lossy best-fit are an artefact of its ANSI `main`, not a behaviour a UTF-16 program can reproduce faithfully. So `UrlPath` encodes each non-ASCII character as uppercase UTF-8 escapes; `OsPath` is unchanged.
- Pinned by `FileUrlPathTests.TryParse_UnescapedNonAsciiCharacter_IsQuotedAsUppercaseUtf8Escapes` (five rows), `..._IsKeptInTheOperatingSystemPath` and `TryParse_NonAsciiBesideAsciiAndAnEscape_EncodesOnlyTheNonAsciiCharacter`.
- Pipeline `feature` delivered in-session: a single private method (`EncodeNonAscii`) in one type, tests first (6 failed red, then green); no architect stage needed for a change this size.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. file:// exit 37 quotes an unescaped non-ASCII character as uppercase UTF-8 escapes, as curl 8.21.0's Unicode build does
