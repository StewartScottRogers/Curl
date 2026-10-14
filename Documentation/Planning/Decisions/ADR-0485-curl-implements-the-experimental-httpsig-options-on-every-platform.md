# ADR-0485 — Curl implements the experimental --httpsig-* options on every platform

- **Status:** Accepted
- **Date:** 2026-10-13

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-2171
(gap finding GF-0040).

## Context

curl 8.22.0 added `--httpsig-algo`, `--httpsig-key`, `--httpsig-keyid` and `--httpsig-headers`,
which sign a request with RFC 9421 HTTP Message Signatures (`ed25519` or `hmac-sha256`, label
`sig1`). All four are `Experimental: yes` and exist only in a build configured with
`--enable-httpsig`. The Windows reference (Git 2.56.0's ucrt64 curl 8.22.0) is built without
them and answers `curl: option --httpsig-algo: is unknown`, exit 2, as Curl does today, so gap
run 2026-10-13_1406 closed GF-0040 on measurement.

The standing rule is a complete reimplementation of curl: nothing is left out because a
reference build lacks it (root `CLAUDE.md`, "Decisions"), and ADR-0454 already keeps `--ech`
and the DNS options for the same reason.

## Decision

Curl implements the four `--httpsig-*` options on every platform, as an intended difference from
the Windows reference build.

- The signing is hand-built in its own `Curl.HttpMessageSignatures.UnitLibrary`, BCL only plus
  `Curl.Cryptography.UnitLibrary`'s Ed25519; HMAC-SHA256 is the BCL's `HMACSHA256`. It is pure
  computation (the caller passes `created`), held to the solution's quality gates, and its tests
  pin RFC 9421 appendix B.2.5 and B.2.6 byte for byte.
- With no reference build to measure, its behaviour is read from upstream `lib/http_httpsig.c`
  and `docs/cmdline-opts/httpsig-*.md` at `curl-8_22_0`: the `sig1` label, the parameter order
  `created`, `keyid`, `alg`, the default components `@method @authority @path` plus `@query` when
  the URL has a query, header components taken only from `-H` lines, no signing when a `-H` line
  sets `Signature` or `Signature-Input`, and every refusal exit 43 with upstream's `httpsig: ...`
  message (or curl's generic text where upstream sets none).
- BL-2180 wires the options into `Curl.Cli`, the HTTP request (with `created` from the injected
  `TimeProvider`), `--help`, the manual and `--ai-help`.

An interactive session is asked to add the eight `options:--httpsig-*` facets of
`--httpsig-algo`, `--httpsig-headers`, `--httpsig-key` and `--httpsig-keyid` to
`Gap/Baselines/option-exclusions.json` with a reason naming this ADR, since a lane may not write
`Gap/` (ADR-0433): once BL-2180 lands, the reference's `is unknown` answer no longer matches
Curl's.

## Consequences

- On Windows, `curl --httpsig-algo ...` signs where the reference curl refuses the option; on a
  Linux or macOS curl built with `--enable-httpsig` the two agree.
- A change to upstream's signing text or messages in a later curl is followed by reading its
  `lib/http_httpsig.c` again, not by measuring a reference.
