# ADR-1273 — --libcurl setopt order follows the vendored 8.22.0 cases, not 8.21.0 (BL-5294)

Status: Accepted. Date: 2026-11-16. Decided by Claude under Stewart's delegation.

## Context

BL-5294 (filed from Surl's measurement beside curl 8.21.0) asks `--libcurl` to write its
`curl_easy_setopt` calls in 8.21.0's order so cases 1402, 1404, 1405, 1406, 1407 and 1465 pass.
Curl reports itself as curl 8.22.0 (`-V`, the User-Agent), and its vendored upstream cases
(`Curl.Conformance.UnitTests/CurlTestCases/Original`, `LIBCURL_VERSION "8.22.0-DEV"`) hold 8.22.0's
`<file>` sections. 19908ab502 (2026-10-21, BL-2966) moved the generator to 8.22.0's
`config2setopts.c` order precisely so these cases pass; the six are on the ratchet for that reason.

## Decision

Keep 8.22.0's order. Moving to 8.21.0's order would make the six cases fail against the data
the repository vendors and contradict the version Curl reports. The difference Surl sees is a
version difference (8.21.0 oracle against an 8.22.0 port), not a defect. The runner is not wrong:
it compares the generated file with the 8.22.0 `<file>` (after `<stripfile>`), and passes.

## Consequences

BL-5294 is deferred. If Curl ever reports itself as 8.21.0, the order moves with the vendored cases.
