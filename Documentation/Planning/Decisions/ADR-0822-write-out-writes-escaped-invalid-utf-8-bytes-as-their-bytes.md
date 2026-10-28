# ADR-0822: -w writes each escaped invalid UTF-8 byte as that byte, on every platform

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3852)
- Relates to: [ADR-0909](ADR-0909-linux-and-macos-read-argv-raw-bytes-with-surrogate-escapes.md), [ADR-0893](ADR-0893-write-out-header-values-keep-their-raw-bytes.md)

## Context

CGR-016 part 4: on Linux and macOS the console hands each argument byte b that is not valid UTF-8 over
as the lone surrogate U+DC00 + b (ADR-0909's argv decoding). `WriteOutTemplateRenderer` encoded its
text with `Encoding.UTF8`, which writes a lone surrogate as U+FFFD, so `-w $'\xfe'` wrote EF BF BD.
Measured with `Record-CurlExchange.ps1 -Curl wsl.exe` against curl 8.18.0 (OpenSSL, Ubuntu in WSL):
`curl -s -o /dev/null -w $'\xfe' URL | od -An -tx1` prints ` fe`.

## Decision

- `WriteOutTemplateRenderer` writes each lone surrogate U+DC80 to U+DCFF as its low byte, as it already
  writes a header byte marked by `MarkHeaderBytes` (U+F780 to U+F7FF). Every other character, a valid
  surrogate pair and any other lone surrogate included, is written as UTF-8 as before.
- The renderer does this on every platform, with no platform switch. On Windows the arguments are
  never escaped (the console decodes argv only off Windows), so a Windows `-w` text holds such a
  surrogate only when the launching process passed ill-formed UTF-16 that no shell produces; every
  `-w` text a Windows shell can pass is written as before. A switch would add a branch the Windows
  test run cannot cover, for a case curl's own Windows build cannot meet in practice.
- `-w @file` (the file's bytes are still decoded as plain UTF-8, BL-3862) and `%output{}` file names
  (BL-3863) are left to their own tasks.

## Consequences

`curl -w $'\xfe'` on Linux and macOS writes the byte FE, as curl 8.18.0 does. Text that is valid UTF-8
is written byte for byte as before.
