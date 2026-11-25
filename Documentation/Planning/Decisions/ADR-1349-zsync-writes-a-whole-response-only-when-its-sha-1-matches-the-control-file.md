# ADR-1349 — `--zsync` writes a whole response only when its SHA-1 matches the control file's, and exits 61 otherwise

- **Status:** Accepted
- **Date:** 2026-11-24

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-5896.

## Context

ADR-1092 says a SHA-1 mismatch runs the transfer again as curl runs it, "so the output is never a wrong file". The
second run's body, and a 200 answering a range request, were written without being checked, so a server or path that
altered both gave a wrong file with exit 0 (fitness finding EXT-121).

## Decision

- Every body `--zsync` writes has the control file's SHA-1: the joined file, a 200 answering a range request, and
  the transfer run again after an unusable range answer or a mismatched joined file.
- A successful whole response (a 200 to a range, or the second run) whose body's SHA-1 differs fails the transfer
  with exit 61 (`BadContentEncoding`, as `--checksum` does) and the message
  `--zsync: SHA-1 mismatch: expected <hex>, got <hex>`. Neither body nor headers are written, so no output file is
  left with wrong bytes. This includes an error page such as a 404 body: it is not the target.
- A 200 answering a range request that mismatches is not asked for again: it already was the whole resource.
- A second run that fails keeps its own exit code, and nothing of it is written.

This replaces ADR-1092's "A 200 to a range (the server ignores `Range`) is written as received".

## Consequences

- The second run's body is held in memory until it is checked, as the joined file already is.
