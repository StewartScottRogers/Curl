# ADR-0960: Off Windows, a host of one-byte characters that are UTF-8 is read as that UTF-8

- Status: Accepted
- Date: 2026-10-28
- Task: BL-3983
- Decided by Claude under Stewart's delegation.

## Context

ADR-0953 pinned how a `Location` host typed as UTF-8 bytes is read on each platform: as
Windows-1252 on Windows, where Curl already matched, and as UTF-8 off Windows, where curl
in a UTF-8 locale hands the header's raw bytes to `idn2_lookup_ul` and follows upstream
test1448 to `xn--4cab6c.se`. A `Location` header's bytes reach `CurlUrl` one Latin-1
character per byte, so off Windows `CurlUrlHost` converted `Ã¥Ã¤Ã¶` and followed to
`xn--bbab6cyjbb.se`; `Parse_LocationHostOfUtf8Bytes_ReadsTheBytesAsUtf8OffWindows` failed
on Linux and macOS in CI.

## Decision

Off Windows, `CurlUrlHost` reads a decoded name whose every character is at most `U+00FF`
and whose Latin-1 bytes are valid UTF-8 as that UTF-8 before IDNA converts it; any other
name is converted as decoded. The fix stays in `CurlUrlHost`, the one place both a command
line host and a `Location` host pass, rather than changing how every protocol hands header
text to `CurlUrl`.

## Consequences

Curl off Windows follows test1448's redirect as curl does. A host typed on the command line
as one-byte characters that happen to spell valid UTF-8 (`Ã¥`, which curl reads as the
four UTF-8 bytes of those two characters) is read as `å` instead; such a host is mojibake
no one types, and the trade keeps the fix inside one library.
