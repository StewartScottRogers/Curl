# ADR-0446: A config file's header, user-agent and referer text keeps its UTF-8 bytes on Windows

- Status: Accepted
- Date: 2026-10-08
- Decided by Claude under Stewart's delegation
- Task: BL-1848 (gap item `behaviour:test470`, GF-0020)

## Context

curl 8.21.0's Windows (Schannel) build gets its command-line arguments in the ANSI code page but
uses a `-K` file's bytes raw. Measured with `Record-CurlExchange.ps1`: a UTF-8 config file holding
`-H "X-A: “quoted”"` and `user-agent = “agent”` makes curl send `E2 80 9C ... E2 80 9D` in both
values, after its `starts with a Unicode character` warning. Curl decoded the file as UTF-8 into
.NET strings, and the request side encodes header text in the ANSI code page on Windows
(`HttpRequestOptions.CommandLineTextEncoding`, ADR-0067), so it sent `93 ... 94`.

## Decision

While a config file is read on a Windows parse, `CommandLineOptions.AsWireText` re-spells a `-H` /
`--proxy-header` value, `-A` and `-e` (`ConfigFileWireText.Respell`): the value's UTF-8 bytes are
decoded in the ANSI code page (`ConfigFileWireText.WindowsAnsiCodePage`, read as
`CredentialEncoding.ForPlatform(true)` reads it), so the request side's ANSI encoder gives the
file's bytes back. The Unicode warning still checks and prints the value as decoded UTF-8. When the
code page cannot carry the bytes unaltered (a double-byte code page), the value is kept as it was.
Off Windows nothing changes: UTF-8 already round-trips.

## Consequences

- Upstream test 470 sends what curl sends on Windows; command-line arguments still go out in the
  ANSI code page.
- Only the HTTP head values are re-spelled, because they are the only option text the request side
  encodes with `CommandLineTextEncoding`. Other config-file values keep their UTF-8 decoding, and a
  `-H @file`'s lines are not changed; a gap measured there is a task of its own.
