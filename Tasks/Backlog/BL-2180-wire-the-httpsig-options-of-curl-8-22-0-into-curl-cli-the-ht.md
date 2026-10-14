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
completed:
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

- [ ] `Curl.Cli.UnitLibrary` parses the four options (single-valued, `@file` for the key,
      algorithm values checked) with curl 8.22.0's error texts and exit codes, read from
      upstream's source, with tests in `Curl.Cli.UnitTests`.
- [ ] An HTTP request sent with the options carries `Signature-Input` and `Signature`; a test in
      `Curl.Protocol.Http.UnitTests` checks the header bytes for a fixed `created` time through an
      injected `TimeProvider`, for ed25519 and hmac-sha256, and that a `-H Signature:` line
      suppresses both.
- [ ] A missing key or key id, a bad hex key and an unknown component end the transfer with
      exit 43 and upstream's message.
- [ ] `--help`, the manual and `--ai-help` list the four options (CLAUDE.md, "`--ai-help` keeps pace").
- [ ] Every changed library stays at 100% line and branch coverage, complexity at most 10, CRAP
      at most 30.

## Notes

## Log

- 2026-10-13: Created.
