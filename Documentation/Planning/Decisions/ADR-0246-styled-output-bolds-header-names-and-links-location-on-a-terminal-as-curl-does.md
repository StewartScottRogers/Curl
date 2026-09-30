# ADR-0246 — `--styled-output` bolds header names and links `Location` on a terminal, as curl does

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-736.

## Context

BL-489 parsed `--[no-]styled-output`; nothing styled. The standing rule (2026-09-28) is that
the switch actually styles. curl 8.21.0 does it in `tool_header_cb` (`src/tool_cb_hdr.c`):
when `-i`/`-I` headers go to the body output, the transfer's scheme is `http`, `https`,
`rtsp` or `file`, standard output is a terminal (`global->isatty`, set when a transfer's body
goes to a terminal standard output), styling is on, and on Windows the console accepted
virtual-terminal processing (`tool_term_has_bold`), each header line with a colon is written
as `ESC[1m<name>BOLDOFF:<value>`. `BOLDOFF` is `ESC[22m` on Windows and `ESC[0m` elsewhere.
Off Windows a `Location` value is also wrapped in an OSC 8 hyperlink (`write_linked_location`)
to the URL it resolves to against the effective URL, when that URL is `http`, `https`, `ftp`
or `ftps`, and not inside VTE 0.48.1 or older (`VTE_VERSION` at most 4801). `-D` output is
never styled. Measured with curl 8.18.0 on Linux under `script` (BL-736 Notes); the
Windows bytes are the source's, as a Windows console cannot be recorded by a pipe.

## Decision

1. `Curl.Output`'s `StyledHeaderLines` styles one line as curl does, bytes included, and
   `StyledHeaderStream` applies it to each line of the header output. The name match for the
   link is curl's `curl_strnequal("Location", name, namelen)`: any case-insensitive prefix of
   `Location`, the empty name included.
2. The runner (`CurlCommandRunner.HeaderStylesFor`) styles only a transfer writing its body to
   standard output, when `standardOutputIsTerminal`, `terminalRendersStyles` and
   `--styled-output` are all on and the scheme is one of curl's four; `TransferContextFactory`
   wraps the body side only, so `-i -D -` prints the `-D` copy plain and the `-i` copy styled.
3. `terminalRendersStyles` is curl's `tool_term_has_bold`: always true off Windows; on Windows
   `StandardOutputVirtualTerminal` turns on `ENABLE_VIRTUAL_TERMINAL_PROCESSING` for a console
   standard output and puts the old mode back when the run ends, as curl does.
4. The link target is resolved with the BCL's `Uri` against the transfer URL (scheme,
   user information, host, non-default port, path and query), and each linked value becomes
   the base of the next, standing in for curl's effective URL across `-L` hops. A value that
   is empty or holds a space or control character is not linked, as curl's URL parser rejects
   it. `Uri`'s normalisation (lowercased host, escaped non-ASCII) may differ from curl's URL
   API in the link target only; the text shown is always the value as received.

## Consequences

Redirected output, `-o` files, `-D` output and `--no-styled-output` are byte-for-byte as
before. A test that wants styled bytes passes `standardOutputIsTerminal: true` and
`terminalRendersStyles: true` to the runner.
