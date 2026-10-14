# ADR-0487 — The --httpsig-* options are parsed as strings and checked when an HTTP transfer is set up

- **Status:** Accepted
- **Date:** 2026-10-13

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-2180.

## Context

ADR-0485 has Curl implement curl 8.22.0's `--httpsig-algo`, `--httpsig-key`, `--httpsig-keyid`
and `--httpsig-headers`. No reference build has them, so their parsing is read from upstream's
source at `curl-8_22_0`:

- `src/tool_getparam.c` reads all four with `getstr(..., DENY_BLANK)`: a string, the last one
  winning, an empty value refused as `blank argument where content is expected`. `--httpsig-key`
  is `ARG_FILE|ARG_CLEAR`, so a value that looks like a flag is warned about as a filename; none
  takes `--no-`.
- `src/config2setopts.c` (`httpsig_setopts`, from `http_setopts`) checks them only when it sets up
  an HTTP transfer: a missing key or key id, or an algorithm other than `ed25519` or
  `hmac-sha256`, is exit 2; a key `@file` is read there, exit 26 when it cannot be opened.
- `src/tool_listhelp.c` lists the four (`auth http`) when the build has HTTPSIG.

BL-2180 first said the algorithm was checked while parsing and every failure was exit 43; the
source says otherwise.

## Decision

- `Curl.Cli` parses the four options as upstream's `tool_getparam.c` does and keeps each verbatim
  (`CommandLineOptions.HttpSignatureAlgorithm`, `HttpSignatureKey`, `HttpSignatureKeyId`,
  `HttpSignatureHeaders`), checking and reading nothing. The setup checks, the `@file` read and the
  signing belong to the HTTP transfer and are BL-2182.
- `--help` lists the four rows as a curl 8.22.0 built with `--enable-httpsig` does. The recorded
  `--help` reference files in `Curl.Cli.UnitTests/HelpReference` (`help-all.txt`, its 40- and
  200-column forms, `help-auth.txt`, `help-http.txt`), recorded from a build without the options,
  gain those four rows, formatted as curl formats every row, so the files stay what an
  HTTPSIG-enabled curl prints.
- `--libcurl` writes nothing for them until BL-2182 sends them; the option-coverage test lists them
  as writing nothing.

## Consequences

- `curl --httpsig-algo bogus URL` is accepted by the parser and refused at transfer setup, as
  upstream does, once BL-2182 lands; until then the options parse and change nothing.
