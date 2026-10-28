# ADR-0870 — Variable content and expanded values carry the argument encoding

- Status: Accepted
- Date: 2026-10-28
- Task: BL-3848
- Decided by Claude under Stewart's delegation.

## Context

curl's Windows build gets its arguments and its environment in the ANSI code page and expands
`--variable` content as bytes. Measured with the local curl 8.21.0 (Schannel, code page 1252) on
2026-10-28 through `Record-CurlExchange.ps1`:

| Command | Sent |
|---|---|
| `--variable v=é --expand-data {{v}}` | body `E9` |
| `--variable v@f.txt --expand-data {{v}}`, f.txt holding `C3 A9` | body `C3 A9` |
| `--variable v=é --expand-data é{{v}}é` | body `E9 E9 E9` |
| `--variable v=é --expand-url-query q={{v}}` | `GET /?q=%E9` |
| `--variable %V --expand-data {{V}}`, `V=é` | body `E9` |
| `--variable v@f.txt --expand-header "X-A: é{{v}}"` | header `X-A: E9 C3 A9` |

Curl stored `=content` and imported values as UTF-8, appended a template's typed text as UTF-8 and
read the result back as UTF-8 (ADR-0064), so it sent `C3 A9` for typed text and `EF BF BD` for a
file byte that is not UTF-8. BL-3844 had kept expanded `-d` values in UTF-8 for that reason
(`ApplyingExpandedValue`).

## Decision

- `=content` is stored as the bytes the argument goes out as (`CommandLineOptions.CommandLineTextBytes`:
  the ANSI code page on Windows, UTF-8 off Windows and in a config file read as UTF-8); an imported
  value is stored in the ANSI code page on Windows; a file's bytes are stored unchanged.
- `VariableExpansion` appends a template's typed text through `CommandLineTextBytes` and reads the
  expanded bytes back with its inverse, `CommandLineOptions.CommandLineText` (the ANSI code page on
  Windows, `InvalidUtf8ByteEscape.Decode` otherwise). The expanded value is then text like any typed
  argument, so every option, not only the `-d` family, sends its bytes unchanged.
  `ApplyingExpandedValue` is removed.
- The leading-Unicode check of a value led by a variable's bytes (BL-1814, upstream test268) reads
  those bytes as UTF-8 through `CommandLineOptions.ValueReadForLeadingUnicodeCheck`.

This supersedes ADR-0064's clause that non-UTF-8 bytes become U+FFFD.

## Consequences

Expanded values match curl byte for byte on Windows and off it. On a Windows host whose ANSI code
page is a double-byte one, file bytes that do not form valid characters in it may not round-trip;
curl itself never decodes them, and no measured host uses such a code page.
