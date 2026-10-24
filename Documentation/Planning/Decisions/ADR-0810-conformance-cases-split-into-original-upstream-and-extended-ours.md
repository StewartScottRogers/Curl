# ADR-0810: Conformance cases split into CurlTestCases/Original (upstream) and CurlTestCases/Extended (ours)

- Status: Accepted
- Date: 2026-10-24
- Decision: Decided by Claude under Stewart's delegation (BL-3462), approved by Stewart 2026-10-24

## Context

All 2072 `test*.curltest` files, upstream's support folders and `Update-UpstreamTestData.ps1` sat in
`Curl.Conformance.UnitTests/UpstreamTestData/`. We want cases of our own, derived from upstream's or newly
written, without them being mixed into the upstream copy that is refreshed from a curl release tag.

## Decision

1. The vendored upstream data moves, unchanged, to `Curl.Conformance.UnitTests/CurlTestCases/Original/`,
   and `UpstreamTestData/` no longer exists. Our own cases live in the sibling `CurlTestCases/Extended/`.
   `Update-UpstreamTestData.ps1` writes only into `Original/`.
2. Our cases are named `ext<number>.curltest`, in upstream's `tests/data` format, so a number never collides
   with an upstream `test<number>.curltest`.
3. They run with `Original/`'s `certs/` as `%CERTDIR` and `Original/`'s root as `%SRCDIR`, through
   `UpstreamConformanceTests.ExtendedCases_RunThroughCurl_AllPass`: each must simply pass.
4. They are not on `PassingUpstreamCases.txt` and not in `UpstreamCases`, so the upstream pass count, the
   ratchet and `UpstreamTestDataTests`' count of 2072 see only `Original/`.

## Consequences

ADR text that names `UpstreamTestData` before this decision is history and keeps the old path.
