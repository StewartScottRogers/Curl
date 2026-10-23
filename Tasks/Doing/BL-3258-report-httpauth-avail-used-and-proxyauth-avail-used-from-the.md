---
id: BL-3258
title: Report HTTPAUTH_AVAIL/USED and PROXYAUTH_AVAIL/USED from the offered auth schemes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3256]
touches: [Curl.Engine.UnitLibrary/TransferInfoFromReport.Auth.cs, Curl.Engine.UnitTests/TransferInfoAuthAndRefererTests.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3258 — Report HTTPAUTH_AVAIL/USED and PROXYAUTH_AVAIL/USED from the offered auth schemes

## Goal

`CURLINFO_HTTPAUTH_AVAIL/USED` and `PROXYAUTH_AVAIL/USED` report upstream's bitmasks, read from the offered-scheme fields BL-3256 adds to `TransferReport`.

## Context

Part of BL-3193. `TransferInfoFromReport.WithCounts` already maps `AuthSchemePicked`/`ProxyAuthSchemePicked` to the USED values; make AVAIL come from the report's offered-scheme fields (BL-3256) instead of the final response's headers. The CLI's `-w` variables reading the same values pick this up. Measure upstream libcurl 8.22.0 first; pin per-platform differences in `[OSCondition]` tests. Tests use fakes or loopback only.

## Acceptance criteria

- [ ] Tests show HTTPAUTH_AVAIL/USED after a 401 offering Basic and Digest with CURLAUTH_ANY, and after plain Basic, match upstream's bitmasks.
- [ ] Tests show PROXYAUTH_AVAIL/USED after a 407 the same way.
- [ ] Build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 0 failing members.

## Notes

- Split from BL-3193 (over its cost cap). No stash was left.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 8442c290 2677bb42 3639a5e6.
