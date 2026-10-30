---
id: BL-4139
title: Extension: Pace retries and parallel transfers by RateLimit headers
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094]
touches: [Curl.Extension.RateLimit.UnitLibrary, Curl.Extension.RateLimit.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4139 — Extension: Pace retries and parallel transfers by RateLimit headers

## Goal

Curl provides an option that reads `RateLimit-Policy`/`RateLimit` and waits accordingly, as the extension `Curl.Extension.RateLimit.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 38 of 63. Specification: draft-ietf-httpapi-ratelimit-headers (latest). It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.RateLimit.UnitLibrary` and `Curl.Extension.RateLimit.UnitTests` exist in `/Extensions/RateLimit/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides an option that reads `RateLimit-Policy`/`RateLimit` and waits accordingly.
- [x] Unit tests check its wire behaviour against named sections of draft-ietf-httpapi-ratelimit-headers (latest), with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 9): Done in one run, ADR-1106. `--ratelimit-pace` is a transfer hook, one per option group: after each HTTP response a `RateLimit` member with `r=0` holds the origin for `t` seconds (§4.1.2), or the same-named `RateLimit-Policy` member's `w` (§3.1.3); the next transfer to that origin (a `--retry` attempt, a later URL or a `-Z` parallel transfer) waits on the transfer's `TimeProvider` (§7). Followed draft-ietf-httpapi-ratelimit-headers-11 (23 May 2026, the latest on datatracker).
- Like ADR-1094's `--content-digest`, the extension has no URL scheme, so it is registered by one line each in ExtensionOptionCatalogue, ExtensionDescriptionCatalogue and ExtensionTransferHookCatalogue (added to `touches`; all three are shared list files) and adds no line to `ExtensionCatalogue.cs`, which holds only protocol handler factories. The first criterion is met in that sense.
- Measured: `Measure-CodeQuality.ps1` on Curl.Extension.RateLimit.UnitLibrary 100% line and branch, worst CRAP 10, 0 failing; Composition 100/100, 0 failing. Built curl lists it in `--extensions`; `CURL_EXTENSIONS=0 curl --ratelimit-pace` exits 2 with "option --ratelimit-pace: is unknown". Fast tests green on Windows; the tests use a fake clock and recorded fields only, nothing platform-dependent.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: f70061d1 81da66f5 f42cb390 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. --ratelimit-pace holds an origin whose RateLimit quota is used up until it resets, for retries and later or parallel transfers
