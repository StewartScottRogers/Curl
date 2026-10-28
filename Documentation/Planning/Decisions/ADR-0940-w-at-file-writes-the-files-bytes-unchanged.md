# ADR-0940 — -w @file writes the file's bytes unchanged

- Status: Accepted
- Date: 2026-10-28
- Task: BL-3862 (CGR-016 part 6)
- Decided by Claude under Stewart's delegation.

## Context

`CommandLineOptionTable.SetWriteOut` read a `-w @file` template with `Encoding.UTF8.GetString`, so a
byte that is not valid UTF-8 became U+FFFD and was written as `EF BF BD`. curl writes the template's
bytes raw. Measured on 2026-10-28 with `Record-CurlExchange.ps1` and a file `fmt` holding `41 FE 42`:
`curl -s -o /dev/null -w @fmt http://<host>/` writes `41 FE 42` with curl 8.18.0 (OpenSSL, WSL Ubuntu)
and `curl -s -o NUL -w @fmt http://127.0.0.1/` writes `41 FE 42` with curl 8.22.0 Schannel on Windows.

The write-out renderer writes a lone surrogate U+DC80 to U+DCFF as its low byte (ADR-0822, BL-3852), and
writes other text in UTF-8 off Windows and in the ANSI code page on Windows.

## Decision

`CommandLineOptionTable.WriteOutFileTemplate` turns the file's bytes (line breaks and NULs removed) into
the template text:

- Off Windows (no config-file wire encoding), the bytes are read as UTF-8 with each invalid byte kept as
  its `InvalidUtf8ByteEscape` surrogate, so the renderer writes it back as that byte.
- On Windows, the raw bytes are spelled in the ANSI code page, one character per byte sequence, when that
  code page gives them back unaltered (every byte does in Windows-1252), so valid UTF-8 and invalid bytes
  alike come out as the file held them; this is `ConfigFileWireText.Respell` over the bytes rather than
  over the UTF-8 of already decoded text, which would have turned `FE` into U+FFFD first. Where the code
  page cannot carry them (a double-byte code page meeting a lone lead byte) it falls back to the escaped
  UTF-8 reading.

## Consequences

`-w @file` matches curl byte for byte on both platforms for a file holding invalid UTF-8.
`Cgr016WriteOutFileTests` pins both platform paths and the fallback.
