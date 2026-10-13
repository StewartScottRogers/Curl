---
id: BL-2171
title: Close GF-0040: implement the experimental --httpsig-* options of curl 8.22.0 (RFC 9421) in a hand-built Curl.HttpMessageSignatures.UnitLibrary
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.HttpMessageSignatures.UnitLibrary, Curl.HttpMessageSignatures.UnitTests, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.slnx, Documentation]
requirement: none
created: 2026-10-13
completed:
---
# BL-2171 — Close GF-0040: implement the experimental --httpsig-* options of curl 8.22.0 (RFC 9421) in a hand-built Curl.HttpMessageSignatures.UnitLibrary
# BL-2171 — Close GF-0040: implement the experimental --httpsig-* options of curl 8.22.0 (RFC 9421) in a hand-built Curl.HttpMessageSignatures.UnitLibrary

## Goal

`curl --httpsig-algo`, `--httpsig-key`, `--httpsig-keyid` and `--httpsig-headers` sign outgoing
HTTP requests with RFC 9421 HTTP Message Signatures as curl 8.22.0 built with
`--enable-httpsig` does, the signing hand-built in its own `Curl.HttpMessageSignatures.UnitLibrary`.

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

- [ ] A new `Curl.HttpMessageSignatures.UnitLibrary` with `Curl.HttpMessageSignatures.UnitTests`
      beside it builds the RFC 9421 signature base and the `Signature-Input` / `Signature`
      headers, hand-built against the BCL only, held to the solution's quality gates (100% line
      and branch coverage, complexity at most 10, CRAP at most 30).
- [ ] Its unit tests pin RFC 9421's own test vectors (Appendix B.2.6 Ed25519 and B.2.5
      HMAC-SHA256) byte for byte.
- [ ] `Curl.Cli.UnitLibrary` parses the four options (single-valued, `@file` for the key,
      algorithm values checked) with curl 8.22.0's error texts and exit codes, measured from
      upstream's source, since no reference build has them.
- [ ] An HTTP request sent with the options carries the signature headers; a test checks the
      header bytes for a fixed `created` time through an injected `TimeProvider`.
- [ ] `--help`, the manual and `--ai-help` list the four options (CLAUDE.md, "`--ai-help` keeps pace").
- [ ] An ADR, "Decided by Claude under Stewart's delegation", records that Curl implements the
      experimental `--httpsig-*` options as an intended difference from the Windows reference,
      and asks an interactive session to add the eight `options:--httpsig-*` facets to
      `Gap/Baselines/option-exclusions.json` (a lane may not write `Gap/`).

## Notes

- Finding: GF-0040, `options:--httpsig-algo`, `options:--httpsig-headers`,
  `options:--httpsig-key`, `options:--httpsig-keyid`; targeted version curl 8.22.0 (ADR-0479).

## Log

- 2026-10-13: Created by hand from GF-0040 after gap run 2026-10-13_1406 (BL-2128).
