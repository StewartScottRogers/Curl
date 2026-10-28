# ADR-0838: On Windows the command line is read through the ANSI code page with best fit

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3544, BL-3843, BL-3892)
- Relates to: [ADR-0741](ADR-0741-windows-arguments-stay-in-the-ansi-code-page-as-the-mingw-reference-curl-reads-them.md), CGR-015 of the 2026-10-25 curl gap report in `Documentation/Reviews`

## Context

Gap report finding CGR-015: the designated Windows reference, the Git for Windows ucrt64 curl 8.22.0 that
`Record-CurlExchange.ps1` runs by default, has an ANSI `argv`. Windows converts the UTF-16 command line to
the system ANSI code page (cp1252 on the measuring machine) with best fit before curl sees it, so `中`
becomes `?`, the full-width digit U+FF10 becomes `0`, and `é` or `“` stay as their cp1252 bytes. ADR-0741
already said Curl models that, but only the libraries that turn command-line text into bytes
(`ConfigFileWireText`, `HttpRequestOptions.CommandLineTextEncoding`, credentials) applied the code page;
everything that used an argument as text - the URL parser, `-o` file names - still saw the UTF-16 original.

Measured on 2026-10-26 with `Record-CurlExchange.ps1` (reference curl, then Curl):

| Command | Reference curl | Curl after this change |
| --- | --- | --- |
| `curl -s -o "<dir>\中.txt" http://127.0.0.1:<port>/` | exit 0, creates `_.txt` | exit 0, creates `_.txt` |
| `curl -sS "http://中文.localhost:<port>/"` | exit 3, `curl: (3) URL rejected: No host present` | the same bytes |
| `curl -sS "http://127.0.0.1:2020<U+FF10>/a"` | connects to port 20200 | connects to port 20200 |

## Decision

1. On Windows `Program.Main` hands the runner the arguments `AnsiCodePageArguments.ForPlatform` gives: each
   converted to the system ANSI code page (`CredentialEncoding.ForPlatform`) and back. .NET's code-page
   encodings use Windows' best-fit tables by default, so no hand-built table is needed
   (`Cgr015ArgumentTests` pins `中` to `?` and U+FF10 to `0` under cp1252). Off Windows the arguments pass
   through unchanged (UTF-8, with ADR-0909's byte escapes).
2. Tests pass the code page in (cp1252 from `CodePagesEncodingProvider`), so they pass on every host.
3. Writing the standard-error lines that echo argument text in the ANSI code page is CGR-015 part 1b
   (BL-3893).

## Consequences

- Command-line text now reaches the rest of Curl already reduced to the ANSI code page; the per-library
  ANSI encodings then give its bytes unchanged, since every character left is one the code page holds.
- On a Windows system whose ANSI code page is UTF-8 (65001) the round trip changes nothing, as it changes
  nothing for the reference.
