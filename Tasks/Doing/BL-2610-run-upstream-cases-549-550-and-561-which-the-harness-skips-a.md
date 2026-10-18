---
id: BL-2610
title: Run upstream cases 549, 550 and 561, which the harness skips as Curl lacks the feature ftp
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2610 — Run upstream cases 549, 550 and 561, which the harness skips as Curl lacks the feature ftp

## Goal

Upstream cases 549, 550 and 561 run rather than skip, and pass or are filed.

## Context

Found by BL-2530 (lib549 port: FTP URL through an HTTP proxy with `CURLOPT_PROXY_TRANSFER_MODE`, 550 and 561 with `CURLOPT_TRANSFERTEXT`). The runner skips all three: `Curl lacks the feature ftp` although FTP cases such as 539 run. Find why the harness's feature list misses `ftp` for these cases' `<features>`, fix it, and hold every case that then passes.

## Acceptance criteria

- [ ] Cases 549, 550 and 561 no longer skip for the ftp feature; each that passes is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` and each that fails is filed with its differing lines.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project covers the fix (ADR-0482), and the cases' output does not depend on the platform.

## Notes

Cause: `UpstreamCurlPlatform` left `ftp` off the feature list, though Curl serves FTP and the harness has an FTP stand-in. Listed it. Newly passing and now on the ratchet: 79, 561, 1059, 1092, 1098, 1106, 1802. Filed: BL-2718 (549, 550: no `;type=` on the proxied FTP URL), BL-2719 (208, 299, 1077). 445 and 753 still skip for imap and Debug.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 7a8036c6 4272a6b9 7d0e6df3.
