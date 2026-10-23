---
id: BL-3275
title: Say that ThrowIfRedirectLimitReached also guards 401 and 407 retries
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Abstractions.UnitLibrary/TransferReport.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3275 — Say that ThrowIfRedirectLimitReached also guards 401 and 407 retries

## Goal

The doc comment on `HttpProtocolHandler.ThrowIfRedirectLimitReached` and the name or doc of `TransferReport.RedirectCount` say that authentication resends (401, 407) count against `--max-redirs`, as the code now does.

## Context

Found by the shift-end review (ADR-0710) of 6b344d38b...af2f6a9bd, type-design-analyzer.

The diff made the 401 and 407 retries (`HttpProtocolHandler.cs` around lines 2576 and 2583) call `ThrowIfRedirectLimitReached` and add to `RedirectsFollowed`, matching curl 8.22.0 (`--max-redirs 0` with `--digest` or `--anyauth` ends with exit 47; pinned by `HttpProtocolHandlerTests.AuthSchemesOffered.cs`). The method's doc comment (around line 2674-2680) still says it refuses "each resend after a 417", so it hides the new callers. `TransferReport.RedirectCount` now also counts authentication resends; check that its doc says so and that its name does not claim only redirects (curl's own `CURLINFO_REDIRECT_COUNT` semantics decide whether the name stays). This is the "say what it does, do what it says" rule.

## Acceptance criteria

- [ ] `ThrowIfRedirectLimitReached`'s doc comment names every kind of resend it guards: redirects, 417 resends, and 401/407 authentication retries.
- [ ] `TransferReport.RedirectCount`'s doc states that authentication resends are counted, with the curl 8.22.0 behaviour it matches.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 38d4d21d 207edf4a 3d836d5e.
