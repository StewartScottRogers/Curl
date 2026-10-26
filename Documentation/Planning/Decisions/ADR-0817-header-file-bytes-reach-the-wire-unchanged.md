# ADR-0817: Header file bytes reach the wire unchanged, with unreadable bytes escaped as lone surrogates

- Status: Accepted
- Date: 2026-10-26
- Task: BL-3549
- Decided by Claude under Stewart's delegation.

## Context

Third-party gap report issue CGR-020: curl 8.22.0 sends the bytes of a header file (`-H @file`,
`--proxy-header @file`, `-F "a=b;headers=@file"`) and of a `-K` file's `cookie` and `request` values
unchanged, never transcoding them (measured on Windows with `Record-CurlExchange.ps1`: `X: C3 A9 E4 B8 AD`,
`X: E9 FF`, `Cookie: c=C3 A9`, request line `C3 A9 /a HTTP/1.1`, part header `X-C: FF 80 C3 28`). Curl
read header files as UTF-8 into a string that the request side encoded in the command-line text
encoding (the ANSI code page on Windows, UTF-8 elsewhere), and wrote `-b` and `-X` one character per
byte, so bytes were lost or re-encoded. Option values are strings end to end, so the bytes must be
carried in a string the request side turns back into them.

## Decision

- `Curl.Cli.HeaderFileText.Read` reads a header file in the request side's encoding when that encodes
  the text back into the file's bytes (the Windows ANSI code page, `ConfigFileWireTextEncoding`), and
  otherwise as UTF-8 with each byte UTF-8 cannot read held as the lone surrogate U+DC00 plus the byte
  (U+DC80 to U+DCFF), the scheme Python calls `surrogateescape`.
- `HttpRequestHeadFormatter.WireText` gives each such lone surrogate back as its byte and encodes the
  rest in the command-line text encoding, as before.
- A `-b` cookie string (a value with `=`) or `-X` method read from a config file is re-spelled one
  character per byte of the file's bytes, since the request side writes those values in Latin-1.

## Consequences

- Every repro of CGR-020 matches curl byte for byte on Windows; off Windows `-H @file` and forward-proxy
  `--proxy-header @file` do too. A multipart part header and a CONNECT proxy header still turn the
  escapes into U+FFFD off Windows until BL-3828 teaches those encoders the same rule.
- A lone surrogate never comes from valid text, so the escape cannot collide with a typed header.
