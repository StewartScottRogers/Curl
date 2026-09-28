# ADR-0079 — `--resolve` and `--connect-to` are kept verbatim by the parser and checked at transfer time

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"). The
decision was taken in BL-202 on 2026-09-26; this record was written in BL-312 because
this folder was held by another lane at the time.

## Context

`--resolve host:port:addr[,addr]` and `--connect-to host:port:host:port` have a syntax
of their own: wildcard hosts, `+` and `-` prefixes, bracketed IPv6 and comma-separated
address lists. `Curl.Cli` had to decide whether to check that syntax while parsing the
command line or leave it to the transfer.

Measured on 2026-09-26 with curl 8.21.0, the mingw Schannel build that is the Windows
reference (ADR-0018), against `http://127.0.0.1:1/` (a closed port, so a value that is
not refused goes on to connect and fails with exit 7 or 28):

| Command | Result |
| --- | --- |
| `--resolve` with `''`, `a:80:127.0.0.1`, `[::1]:80:127.0.0.1`, `+a:80:127.0.0.1`, `*:80:127.0.0.1`, `-a:80`, `a:80:127.0.0.1,[::1]` | Goes on to connect (exit 7 / 28). |
| `--resolve` with `garbage`, `a:x:1.2.3.4`, `a:80:`; `--resolve=x` | Exit 49, `curl: (49) Could not parse CURLOPT_RESOLVE entry 'garbage'` (the entry named in quotes) - a libcurl error raised when the transfer starts, not a command-line refusal. |
| `--connect-to` with `''`, `garbage`, `a:80:127.0.0.1`, `::127.0.0.1:`, `[::1]:80:[fe80::1]:8080`, `example.com::other.example:` | Goes on to connect. |
| `--resolve` / `--connect-to` with no value | Exit 2, `curl: option --resolve: requires parameter` and the try-help line. |
| `--no-resolve` / `--no-connect-to` | Exit 2, `curl: option --no-resolve: the given option cannot be reversed with a --no- prefix` and the try-help line. These are the only `--no-` spellings. |

So curl's tool layer refuses nothing but a missing value and the `--no-` spellings: it
appends each value to an slist, and libcurl parses the list when the transfer starts.

## Decision

The parser stores every `--resolve` and `--connect-to` value verbatim, the empty string
included, in command-line order, in `CommandLineOptions.ResolveEntries` and
`CommandLineOptions.ConnectToEntries` (`Curl.Cli.UnitLibrary`), and never refuses one.
Its only refusals are the two curl makes: a missing value and the `--no-` spellings, each
exit 2 with curl 8.21.0's exact lines (tests in
`Curl.Cli.UnitTests/CommandLineResolveOptionTests.cs`).

The syntax is checked when a transfer starts, where curl checks it: `ResolveOverrides`
and `ConnectToMappings` in `Curl.Networking.UnitLibrary` (BL-214) parse the verbatim
values and fail with exit 49 and curl's message; composing them into the connectors in
`Curl.Console` is BL-244.

Precedent: `-t` / `--telnet-option` (BL-038, BL-044) is kept verbatim in
`CommandLineOptions.TelnetOptions` for the same reason - curl refuses a bad telnet option
with exit 48 or 49 after connecting, not while parsing.

## Consequences

- Exit codes and ordering match curl: a malformed `--resolve` entry exits 49 at transfer
  time, after anything curl does before the transfer, never 2 at parse time.
- The parser stays a table of arities; it needs no knowledge of host, port or address
  syntax.
- The syntax lives in one place, the transfer layer, so the parser and the connector
  cannot disagree about it.
- A malformed value is not reported until a transfer starts, so the parser's tests cannot
  catch one; the transfer layer's tests (`ResolveOverridesTests`, `ConnectToMappingsTests`)
  must.

## Alternatives considered

- **Split and validate in the parser.** Refuses too early and with the wrong exit code
  (2 rather than 49), unlike curl 8.21.0.
- **Split in the parser, refuse later.** Two parsers of the same syntax, or a structured
  type in `Curl.Cli` that the transfer layer must reinterpret anyway; nothing gained over
  handing the transfer layer the verbatim strings.
