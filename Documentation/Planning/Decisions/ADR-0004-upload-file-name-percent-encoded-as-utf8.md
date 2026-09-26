# ADR-0004 — The `-T` file name appended to a URL is percent-encoded as UTF-8

- **Status:** Proposed
- **Date:** 2026-09-26

## Context

When `-T`/`--upload-file` targets a URL whose path names no file (it ends in `/` or is
absent), curl appends the local file's base name to the URL before the transfer
starts, percent-encoding it with `curl_easy_escape`. Upstream: the
[`-T`/`--upload-file` manpage entry](https://curl.se/docs/manpage.html#-T) and
[`curl_easy_escape`](https://curl.se/libcurl/c/curl_easy_escape.html), with the
behaviour in `src/tool_operhlp.c` (`add_file_name_to_url`), all checked against curl
8.21.0.

`curl_easy_escape` encodes bytes, so the result depends on which bytes the file name
is in, and that differs between curl builds. Measured on curl 8.21.0 with the file
name `é.txt`:

| Build | Appended name |
| --- | --- |
| Microsoft's Windows build in `System32` | `%C3%A9.txt` (UTF-8) |
| Linux and macOS in a UTF-8 locale | `%C3%A9.txt` (UTF-8) |
| mingw / MSYS2 builds without Unicode support | `%E9.txt` (the ANSI code-page byte) |

A drop-in replacement has to pick one, because .NET hands the name over as a UTF-16
`string` with no build-dependent byte form.

The resolution is implemented as `UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile` in
`Curl.Cli.UnitLibrary`. It splits the URL textually and does not parse it.

## Decision

The base name is always percent-encoded as UTF-8, with `Uri.EscapeDataString`. Its
unreserved set (`A-Z a-z 0-9 - . _ ~`) is the one `curl_easy_escape` leaves
unencoded, so ASCII names encode byte-for-byte as curl encodes them, and non-ASCII
names match the official Windows build and UTF-8 Linux and macOS.

URL parsing, validation and normalisation are not part of this resolution. A malformed
URL (exit 3, `CURLE_URL_MALFORMAT`), scheme guessing that adds `http://` to a URL with
no scheme, and slash or dot-segment normalisation all belong to the URL layer (see
ADR-0003's known limitation and task BL-010). When the resolution is wired into the
option parser and transfer dispatcher, that call point must run the URL layer on the
result, so a URL that reaches a protocol handler has been through both.

## Consequences

Good:

- Output is the same on every platform, and matches the curl builds most users
  have: the one Windows ships and the Linux and macOS packages.
- The resolution is pure string work with no locale or code-page dependency, so it
  is unit tested without the file system or the network.

Costs and caveats:

- Scripts written against a non-Unicode mingw or MSYS2 build of curl, which send the
  ANSI code-page byte, get a different URL for a non-ASCII file name.
- Because the resolution does not validate, a malformed URL passes through it
  unchanged. Until the URL layer runs at the call point, nothing reports exit 3 for it.

## Alternatives considered

- **Encode with the active ANSI code page on Windows.** Rejected: it matches only the
  non-Unicode builds, not the one Windows itself ships, and makes the output depend on
  machine settings.
- **Encode with the platform's file-name encoding.** Rejected: .NET does not expose a
  per-platform byte form of a file name, and every platform it would reproduce already
  uses UTF-8.
- **Parse the URL with `System.Uri` before appending.** Rejected: `Uri` normalises and
  rejects URLs curl accepts (ADR-0003, known limitation), and validation belongs to the
  URL layer, not to this one step.
