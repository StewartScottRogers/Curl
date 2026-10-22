---
id: BL-2180
title: Wire the --httpsig-* options of curl 8.22.0 into Curl.Cli, the HTTP request and the help, signing through Curl.HttpMessageSignatures.UnitLibrary
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2171]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Documentation]
requirement: none
created: 2026-10-13
completed: 2026-10-13
---
# BL-2180 — Wire the --httpsig-* options of curl 8.22.0 into Curl.Cli, the HTTP request and the help, signing through Curl.HttpMessageSignatures.UnitLibrary

## Goal

`curl --httpsig-algo`, `--httpsig-key`, `--httpsig-keyid` and `--httpsig-headers` sign outgoing
HTTP requests with RFC 9421 HTTP Message Signatures as curl 8.22.0 built with
`--enable-httpsig` does, through BL-2171's `Curl.HttpMessageSignatures.UnitLibrary`.

## Context

Split out of BL-2171 (GF-0040) on 2026-10-13: BL-2171 built the signing library
(`CurlHttpSignature.CreateHeaders` and its parts; read that project's README and the ADR
BL-2171 added) and this task wires it in. Upstream facts, read from `lib/http_httpsig.c` and
`docs/cmdline-opts/httpsig-*.md` at `curl-8_22_0` (no reference build has the options):

- All four options: Category `auth http`, Multi single, Experimental yes, Protocols HTTP.
  Help lines: `--httpsig-algo <algorithm>` "Algorithm for HTTP Message Signatures",
  `--httpsig-key <key/file>` "Key for HTTP Message Signatures", `--httpsig-keyid <id>`
  "Key identifier for HTTP Message Signatures", `--httpsig-headers <components>`
  "Components to sign for HTTP Message Signatures".
- Signing is on when any of the four is given; the algorithm defaults to ed25519. `--httpsig-key`
  takes hex or `@filename` (file read as the key text). Any algorithm other than `ed25519` or
  `hmac-sha256` is an error: read upstream `src/tool_getparam.c` and `src/tool_operate.c` at
  `curl-8_22_0` for the exact text and exit code before pinning it.
- Library-side failures are all `CURLE_BAD_FUNCTION_ARGUMENT` (exit 43) with the
  `HttpSignatureException` message (`httpsig: ...`), including `httpsig: CURLOPT_HTTPSIG_KEY is
  required` and `httpsig: CURLOPT_HTTPSIG_KEYID is required` when only some options are given.
- Header components come only from the `-H` lines (`data->set.headers`), not headers curl adds.
  `@authority` is the Host header value when one is sent, else the host, plus `:port` when the
  port is not the scheme's default. `@path` is the URL path (`/` when empty); the raw query
  decides whether the default set includes `query`.
- When a `-H` line already sets `Signature` or `Signature-Input`, curl adds neither.
- Otherwise curl adds `Signature-Input: sig1=...` then `Signature: sig1=:...:`, in the auth header
  slot (`data->req.hd_auth`), and verbose prints `* httpsig: Signature-Input params: ...`,
  `* httpsig: Signature base: [...]`, `* httpsig: Signature-Input: ...` and `* httpsig: Signature: ...`.
- `created` is the wall clock in Unix seconds: take it from the injected `TimeProvider`.
- Upstream's `tests/data` has httpsig cases (test5008 to test5020 at least: missing key,
  unsupported algorithm, non-existent key file, duplicate component, legacy `@` component
  syntax), skipped in `Curl.Conformance.UnitTests` today; they and their keys
  (`tests/data/data-httpsig-ed25519.key`, `data-httpsig-hmac-sha256.key`) pin the CLI texts
  and header bytes. Debug builds of curl take `created` from `CURL_HTTPSIG_CREATED` when
  `CURL_FORCETIME` is set, which is how those cases fix it.

## Acceptance criteria

- [x] `Curl.Cli.UnitLibrary` parses the four options (single-valued, the last winning, an empty
      value refused as blank, as upstream's `getstr(..., DENY_BLANK)` does), with tests in
      `Curl.Cli.UnitTests`.
- [x] `--help`, the manual and `--ai-help` list the four options (CLAUDE.md, "`--ai-help` keeps pace").
- [x] Every changed library stays at 100% line and branch coverage, complexity at most 10, CRAP
      at most 30.

## Notes

- 2026-10-13 (lane 1): Split. Upstream (`src/tool_getparam.c`, `src/config2setopts.c` at
  `curl-8_22_0`) parses all four options as plain strings refusing blank; the `@file` key read
  and the algorithm check happen at setup time, per HTTP transfer, with exit 2 (missing
  key/keyid, unknown algorithm) and 26 (unopenable key file) - not exit 43 as this task first
  said. That setup check, the signing in the request, verbose lines and redirect rule are
  BL-2182, filed with every upstream fact found here; this task keeps the parsing and the help.
- `--httpsig-key` is a `FileName` row (upstream `ARG_FILE`: flag-like filename warning); the other
  three are `Text` rows. None takes `--no-`. Decision and the help-reference update in ADR-0487.
- The manual already held the four sections; `--help` and `--ai-help` come from `CurlHelpTable`,
  which gained the four rows of upstream's `tool_listhelp.c`. The recorded `--help` reference
  files gained the same rows, as an HTTPSIG-enabled curl prints them.
- `--libcurl` writes nothing for them yet (listed in `LibcurlSourceCodeOptionCoverageTests`'s
  writes-nothing set); BL-2182 sends them.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary`: 100% line, 100% branch,
  0 failing members. Fast tests all green.

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. curl parses --httpsig-algo, --httpsig-key, --httpsig-keyid and --httpsig-headers as curl 8.22.0 does and lists them in --help and --ai-help; signing moved to BL-2182
