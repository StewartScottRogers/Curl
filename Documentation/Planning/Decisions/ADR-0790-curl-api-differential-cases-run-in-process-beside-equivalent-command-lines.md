# ADR-0790: Curl.Api differential cases run in process beside their equivalent command lines

- **Status:** Accepted
- **Date:** 2026-10-23

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-3242
(split from BL-3217, "CI differential harness"; the runner is ADR-0782's).

## Context

The tester's harness compared only command lines. BL-3242 asks for `Curl.Api` requests replayed
beside the equivalent curl 8.22.0 command lines on BL-3232's loopback routes, comparing wire bytes
and results. `Curl.Api` is a library, not a binary, so its side cannot be a process run, and its
results are a `CurlResponse` or a `CurlException`, not standard output and an exit code.

## Decision

- `Curl.Conformance.Differential.UnitLibrary/Api` holds the cases as data with no reference to
  `Curl.Api`, so the library keeps its gates: `ApiDifferentialRequest` describes the request,
  `ApiDifferentialCase` pairs it with the curl command line, and `ApiDifferentialCases.All` lists
  them (GET, custom and empty headers, User-Agent, form posts, multipart, PUT, DELETE, redirects with
  and without following, Basic and Digest authentication, a `Cookie` header and the cookie engine
  across a redirect), each mirroring a CLI HTTP case.
- The reference command line is the case's with `-A '' -w '\n%{response_code}'` before it:
  `-A ''` because only the curl tool sends a `User-Agent` by default (libcurl and so `Curl.Api`
  send none), and the response code makes the result comparable. `ApiDifferentialOutput.Render`
  writes the `Curl.Api` side the same way: the body read to its end, a newline, the status code in
  three digits (`000` for none). The `Curl.Api` side's exit code is 0, or the `CurlException`'s
  `CurlCode` as a number, which is the exit code curl gives for it; a timeout is no exit code.
- `Curl.Conformance.Differential.IntegrationTests/Api` maps each request onto a `CurlRequest`
  (`ApiRequestMapping`), sends it on a new `CurlClient` in process (`ApiCaseRunner`), fetches the
  wire log from the control port as `DifferentialCaseRunner` does, and compares and reports with
  the existing normaliser, comparer and report writer. The project now references `Curl.Api`. The
  test is Integration and inconclusive unless `CURL_DIFFERENTIAL_REFERENCE` names the reference.

## Consequences

- An empty-valued `CurlRequest` header is `Name;` (sent with no value), so its equivalent is
  `-H 'Name;'`; `Curl.Api` cannot remove a default header as `-H 'Name:'` does, filed as BL-3289.
- New request shapes are new lines in `ApiDifferentialCases.All`, and new properties on
  `ApiDifferentialRequest` with their mapping in `ApiRequestMapping`.
