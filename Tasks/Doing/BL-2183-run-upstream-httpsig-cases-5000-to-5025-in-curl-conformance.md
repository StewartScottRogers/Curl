---
id: BL-2183
title: Run upstream httpsig cases 5000 to 5025 in Curl.Conformance.UnitTests with their key files and created=0
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-2182]
touches: [Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-13
completed:
---
# BL-2183 — Run upstream httpsig cases 5000 to 5025 in Curl.Conformance.UnitTests with their key files and created=0

## Goal

Upstream curl 8.22.0's httpsig cases 5000 to 5025 run in `Curl.Conformance.UnitTests` and the
ones Curl passes are in `PassingUpstreamCases.txt`.

## Context

BL-2182 sends the `--httpsig-*` options' `Signature-Input` and `Signature` headers through
`Curl.Console`'s `HttpSignatureHttpAuthenticator`, and refuses bad options as upstream's
`config2setopts.c` does. The cases still cannot run here: `UpstreamTestData` has no
`data/data-httpsig-ed25519.key` or `data/data-httpsig-hmac-sha256.key` (copy them from the
`curl-8_22_0` tarball's `tests/data`), and upstream's debug build fixes `created` to 0 through
`CURL_FORCETIME` and `CURL_HTTPSIG_CREATED`, which the harness must map onto the signing clock
`CurlComposition.CreateRunner(signingClock:)` takes. The ed25519 key in
`Curl.HttpMessageSignatures.UnitTests` already reproduces test 5001's signature (BL-2182); the
HMAC key there does not reproduce test 5002's, so use the tarball's file.

## Acceptance criteria

- [ ] Both key files are in `Curl.Conformance.UnitTests/UpstreamTestData/data`.
- [ ] The cases 5000 to 5025 Curl passes are listed in `PassingUpstreamCases.txt`; each one it
      fails is named under Notes with its first difference, or filed as a task.

## Notes

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
