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
completed:
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

- [ ] The encoding curl 8.21.0 applies to an unescaped non-ASCII character is measured for a
      code page 1252 character and one outside it, and recorded in `Notes`.
- [ ] A named test in `FileUrlPathTests` pins `UrlPath` for each measured case.
- [ ] `dotnet test Curl.Protocol.File.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
