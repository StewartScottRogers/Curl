# ADR-0828: -w writes command-line text in the ANSI code page on Windows

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3841, CGR-015 part 4)
- Relates to: [ADR-0820](ADR-0820-gopher-dict-pop3-mqtt-send-command-line-text-in-the-ansi-code-page-on-windows.md), [ADR-0816](ADR-0816-write-out-header-values-keep-their-raw-bytes.md), [ADR-0822](ADR-0822-write-out-writes-escaped-invalid-utf-8-bytes-as-their-bytes.md)

## Context

CGR-015 found that Curl writes non-ASCII command-line text as UTF-8 on Windows, where the reference
curl (Git for Windows ucrt64 curl 8.22.0, an ANSI-argv build) receives its arguments in the system
ANSI code page and writes them as those bytes. BL-3544 decided Curl matches the reference on Windows
and split the fix by library; this is `-w`'s part.

Measured on 2026-10-26 with `Record-CurlExchange.ps1` (code page 1252, port P):

| Command | Bytes real curl wrote to standard output |
| --- | --- |
| `curl -s -o NUL -w "café ü €\n" http://127.0.0.1:P/café` | `63 61 66 E9 20 FC 20 80 0D 0A` |
| `curl -s -o NUL -w "[é]" http://127.0.0.1:P/café` | `5B E9 5D` |
| `curl -s -o NUL -w "%{url_effective}" http://127.0.0.1:P/café` | `http://127.0.0.1:P/caf` then `E9` |
| `curl -s -o NUL -w @wo.txt http://127.0.0.1:P/café`, `wo.txt` holding `5B C3 A9 5D` | `5B C3 A9 5D` |

A `-w @file` is written as the file's bytes, whatever the code page.

## Decision

- `WriteOutTemplateRenderer` takes the text encoding it writes in. `CurlCommandRunner` passes the
  system ANSI code page on Windows (`ConfigFileWireText.WindowsAnsiCodePage`, the hook the parser
  uses) and UTF-8 elsewhere, so off Windows nothing changes. Header bytes marked by
  `MarkHeaderBytes` and the escaped invalid argument bytes of ADR-0822 stay raw bytes. The byte
  limits of a variable name, header name and `%output{}` file name are counted in that encoding,
  as curl counts its argument bytes.
- File bytes are kept apart from command-line text in the parser, the one place that knows where
  the template came from: on Windows a `-w @file` template, read as UTF-8, is re-spelled in the
  ANSI code page (`ConfigFileWireText.Respell`, BL-1848's mechanism), so encoding it gives the
  file's bytes back; a `-w` in a UTF-8 config file goes through `CommandLineOptions.AsWireText`
  for the same reason. An argument stays as typed.
- Upstream cases 2046 and 2047 write an IDN URL given as UTF-8 bytes through `%{url_effective}`;
  upstream's runner hands curl UTF-8 argv, which the Windows reference never gets. They stay on
  `PassingUpstreamCases.txt` but are not held on Windows (`NeedsUtf8ArgumentsCases`); Linux and
  macOS judge them.

## Consequences

Windows `-w` output matches the reference byte for byte for Latin text. A character the code page
lacks is written as `?`, as the ANSI argv curl receives it. Values that came from the server and are
not marked as header bytes are also written in the code page; none of those carries non-ASCII text
today outside the marked header values.
