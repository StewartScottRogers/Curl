# ADR-0843: On Windows, -o, -T and config-file output names take the mingw curl's ANSI bytes

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3900)
- Relates to: [ADR-0838](ADR-0838-windows-reads-the-command-line-through-the-ansi-code-page.md), [ADR-0840](ADR-0840-a-refused-command-line-echoes-argument-text-in-the-ansi-code-page-on-windows.md), ADR-0446, CGR-114 of the 2026-10-25 curl gap report in `Documentation/Reviews`

## Context

The Windows reference curl (ucrt64 curl 8.22.0, a non-UNICODE build) holds its arguments as ANSI code page
bytes and reads a config file's bytes as they are, and it opens files with the ANSI file API, so a file name
is those bytes read in the ANSI code page. Measured 2026-10-26 on a Windows-1252 host with
`Record-CurlExchange.ps1` (BL-3900 Notes):

- `curl -s -o 日本語.bin http://127.0.0.1:18390/bin` exits 0 and writes `___.bin` (best fit gives `???`, and
  the output-name sanitizer turns `?` into `_`).
- `curl -s -T 日本.txt http://127.0.0.1:18391/echo` exits 26 with `curl: cannot open '??.txt'`.
- A UTF-8 config file holding `output = "café.bin"` with `curl -s -K c.cfg ...` exits 0 and creates
  `cafÃ©.bin`, the bytes C3 A9 read in Windows-1252.

Curl already matched the first two, since `AnsiCodePageArguments` reads every argument through the code page
(ADR-0838); it created `café.bin` for the third.

## Decision

Every `-o` / `--output` and `-T` / `--upload-file` name from the command line takes the ANSI code page's
best-fit text (ADR-0838, unchanged). An `output` read from a UTF-8 config file on Windows is re-spelled with
`ConfigFileWireText.Respell` in `CommandLineOptions.AddOutputFile`, so it is the file's bytes read in the ANSI
code page, as the reference opens it. This is the same re-spelling ADR-0446 and its successors give header,
user-agent, referer and `-w` text from a config file: there it makes the file's bytes go out unchanged; for a
file name it makes the name the one the ANSI file API sees. Only `output` is re-spelled among config-file
file names: it is the one CGR-114 measured, and others (`upload-file`, `cookie-jar`, `dump-header`, and so
on) are left as UTF-8 until a measurement shows them; a file read as ANSI already (not valid UTF-8) is not
re-spelled. Linux and macOS are unchanged: arguments and file names are bytes there.

## Consequences

- The three CGR-114 repros give the reference's exit code, stderr text and file name on Windows.
- A UTF-8 config file's non-ASCII `output` name is mojibake on Windows, as with the reference; a UNICODE
  build (System32 curl 8.21) would write `café.bin`, which Curl no longer does.
- Tests read the code page as the parser does (Windows-1252 where the host names none) or inject
  Windows-1252, so they pass on every host.
