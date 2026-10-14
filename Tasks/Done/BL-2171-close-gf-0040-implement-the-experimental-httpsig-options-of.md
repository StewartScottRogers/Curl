---
id: BL-2171
title: Close GF-0040: implement the experimental --httpsig-* options of curl 8.22.0 (RFC 9421) in a hand-built Curl.HttpMessageSignatures.UnitLibrary
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.HttpMessageSignatures.UnitLibrary, Curl.HttpMessageSignatures.UnitTests, Curl.slnx, Documentation]
requirement: none
created: 2026-10-13
completed: 2026-10-13
---
# BL-2171 — Close GF-0040: implement the experimental --httpsig-* options of curl 8.22.0 (RFC 9421) in a hand-built Curl.HttpMessageSignatures.UnitLibrary
# BL-2171 — Close GF-0040: implement the experimental --httpsig-* options of curl 8.22.0 (RFC 9421) in a hand-built Curl.HttpMessageSignatures.UnitLibrary

## Goal

A hand-built `Curl.HttpMessageSignatures.UnitLibrary` produces the RFC 9421 `Signature-Input`
and `Signature` headers exactly as curl 8.22.0 built with `--enable-httpsig` does, ready for
BL-2179 to wire `curl --httpsig-algo`, `--httpsig-key`, `--httpsig-keyid` and `--httpsig-headers`
to it.

## Context

Gap finding GF-0040 (gap office, ADR-0433; retargeted to 8.22.0 by ADR-0479, BL-2128): curl
8.22.0 added four options, all `Experimental: yes`, built only with `--enable-httpsig`
(upstream `docs/EXPERIMENTAL.md`, "HTTP Message Signatures (RFC 9421)"). The gap run
2026-10-13_1406 closed GF-0040 on measurement because the Windows reference (Git 2.56.0's
ucrt64 curl 8.22.0) is built without it and answers `curl: option --httpsig-algo: is unknown`,
exit 2, exactly as Curl does today. The standing rule (root `CLAUDE.md`, "Decisions": a
complete reimplementation, nothing left out because a reference build lacks it) and ADR-0454's
precedent (`--ech`, the DNS options) make the options an intended difference: Curl implements
them on every platform. This task is that work; it is filed by hand because the finding closed.

Upstream documents at `curl-8_22_0` (lanes cannot read `Gap/`, so the facts are here):

- `docs/cmdline-opts/httpsig-algo.md`: `<algorithm>`, Category `auth http`, Multi single.
  Supported values **ed25519** (default) and **hmac-sha256**; any other value is an error.
  Signing is enabled when any of the four options is given; then `--httpsig-key` and
  `--httpsig-keyid` are required. Default signed components: `method`, `authority`, `path`,
  and `query` when a query string is present; `--httpsig-headers` overrides the set.
- `docs/cmdline-opts/httpsig-key.md`: `<key/file>`, the key as hex digits on one line, or
  `@filename`. ed25519: the 32-byte private seed (64 hex characters); hmac-sha256: the shared
  secret. PEM is not supported.
- `docs/cmdline-opts/httpsig-keyid.md` and `docs/cmdline-opts/httpsig-headers.md`: the `keyid`
  parameter and the component list.
- RFC 9421: the signature base (section 2.5), `Signature-Input` and `Signature` header fields
  (section 4), derived components `@method`, `@authority`, `@path`, `@query`.
- Upstream source: `lib/httpsig.c` at `curl-8_22_0` for the exact header text, parameter order
  (`created`, `keyid`, `alg`) and error messages and exit codes.

Ed25519 and HMAC primitives already exist in `Curl.Cryptography.UnitLibrary` (`Ed25519.cs`,
`FixedBlockHmac.cs`); reuse them or the BCL's `HMACSHA256`.

## Acceptance criteria

- [x] A new `Curl.HttpMessageSignatures.UnitLibrary` with `Curl.HttpMessageSignatures.UnitTests`
      beside it builds the RFC 9421 signature base and the `Signature-Input` / `Signature`
      headers, hand-built against the BCL only, held to the solution's quality gates (100% line
      and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Its unit tests pin RFC 9421's own test vectors (Appendix B.2.6 Ed25519 and B.2.5
      HMAC-SHA256) byte for byte.
- [x] The library reproduces upstream `lib/http_httpsig.c` at `curl-8_22_0`: the `sig1` label,
      the parameter order `created`, `keyid`, `alg`, the default component set, the
      `--httpsig-headers` component parsing and the `httpsig: ...` error texts, each pinned by a test.
- [x] An ADR, "Decided by Claude under Stewart's delegation", records that Curl implements the
      experimental `--httpsig-*` options as an intended difference from the Windows reference,
      and asks an interactive session to add the eight `options:--httpsig-*` facets to
      `Gap/Baselines/option-exclusions.json` (a lane may not write `Gap/`).

## Notes

- Finding: GF-0040, `options:--httpsig-algo`, `options:--httpsig-headers`,
  `options:--httpsig-key`, `options:--httpsig-keyid`; targeted version curl 8.22.0 (ADR-0479).
- 2026-10-13, split before any code: one run cannot build the library, the CLI options, the HTTP
  wiring and the help. This task keeps the library and the ADR; BL-2179 (depends on this one)
  takes the CLI parsing, the HTTP request headers with a `TimeProvider`-fixed `created`, and
  `--help`/manual/`--ai-help`, with the upstream facts it needs in its Context. `touches` narrowed
  to match.
- Upstream source is `lib/http_httpsig.c` (not `lib/httpsig.c`) at `curl-8_22_0`.
- Design: `CurlHttpSignature.CreateHeaders` is the whole of upstream's `Curl_output_httpsig` past
  the transfer state - option checks, the `Signature`/`Signature-Input` opt-out, key decoding,
  the ed25519 length check, component parsing, `sig1` parameters, base and signature - taking
  `created` as an argument so BL-2179 supplies it from `TimeProvider`. Its parts
  (`HttpSignatureBase`, `HttpMessageSigner`, `HttpSignatureHeaders.Create`) take any label and an
  optional `alg`, which is what lets the RFC vectors (label `sig-b25`/`sig-b26`, no `alg`) be
  pinned through the same code. HMAC-SHA256 is the BCL's `HMACSHA256`; Ed25519 is
  `Curl.Cryptography.Ed25519`.
- Choices, with why: every refusal is `HttpSignatureException` (exit 43, `ExitCode`), because
  upstream returns `CURLE_BAD_FUNCTION_ARGUMENT` for all of them; where upstream fails without a
  `failf` (a quote or backslash in `--httpsig-headers`, a control character in a quoted value) the
  message is curl's generic `A libcurl function was given a bad argument`, which the tool prints
  when the error buffer is empty. When a header component is missing upstream calls `failf`
  twice; the first message (`httpsig: header '<name>' not found in request`) is the one the error
  buffer keeps, so that is the one thrown. Lower-casing is ASCII-only, as `Curl_strntolower` is.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.HttpMessageSignatures.UnitLibrary`: 100% line,
  100% branch, 37 members, 0 failing, worst CRAP 8 (the first run found `Parse` at complexity 12;
  split into `DefaultComponents` and `ReadComponents`). 61 tests; full fast run green.
- ADR-0484 records the decision and asks an interactive session to add the eight
  `options:--httpsig-*` facets to `Gap/Baselines/option-exclusions.json`.

## Log

- 2026-10-13: Created by hand from GF-0040 after gap run 2026-10-13_1406 (BL-2128).
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. Curl.HttpMessageSignatures.UnitLibrary signs requests as curl 8.22.0's --httpsig-* do (RFC 9421 B.2.5/B.2.6 pinned byte for byte); wiring in BL-2179
