---
id: BL-5035
title: Look up netrc credentials for a redirect target host when -u is also given
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-5016]
touches: [Curl.Engine.UnitLibrary/TransferCredentialLookup.cs, Curl.Engine.UnitTests/TransferCredentialLookupRedirectNetrcTests.cs, Curl.Console/CurlCommandRunner.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Preparation.cs]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-5035 — Look up netrc credentials for a redirect target host when -u is also given

## Goal

With `-u`, a netrc option and `-L`, each redirect hop to a different host looks up that host's own netrc entry and sends it, as REF does.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip; REF = curl 8.22.0 Schannel, REF2 = curl 8.18.0 OpenSSL), finding STD-040 (Low, Confirmed).

- **Repro:** two echo servers on 127.0.0.1:42001 and :42002 (path `/r?status=302&h=Location:<url>` returns a 302 with that Location; any other path echoes the request line and headers). `printf 'machine a.test login nuser password npass\nmachine b.test login buser password bpass\n' > netrc`, then `curl -s --resolve a.test:42001:127.0.0.1 --resolve b.test:42002:127.0.0.1 -L -u user:pass --netrc-optional --netrc-file netrc "http://a.test:42001/r?status=302&h=Location:http://b.test:42002/echo"`
- **Expected (REF):** the first hop sends `Authorization: Basic dXNlcjpwYXNz`; the hop to b.test sends `Authorization: Basic YnVzZXI6YnBhc3M=` (b.test's own netrc entry).
- **Actual (PORT):** the first hop matches; the b.test hop sends no Authorization header. Without `-u`, PORT looks up netrc for each hop as REF does. Not a leak: PORT is stricter than REF.
- **Where to start:** `Curl.Engine.UnitLibrary/TransferCredentialLookup.cs`, `ForRedirectHops`, which returns null when `UserOptionWins(authentication)`.

## Acceptance criteria

- [x] A new test file `Curl.Engine.UnitTests/TransferCredentialLookupRedirectNetrcTests.cs` asserts that `ForRedirectHops` with `-u user:pass`, a netrc entry for the redirect target host and `-L` returns `buser`/`bpass` for the b.test hop, while the first hop keeps `user:pass`.
- [x] Regression: a redirect target with no netrc entry sends no credentials; the cross-host credential-dropping rules for `-u` without a netrc option are unchanged; without `-u`, per-hop netrc lookup is unchanged.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text. Do not weaken the existing rule that `-u` credentials are not sent to a different host without `--location-trusted`; only the netrc entry of the new host is added.

- 2026-11-12 (lane 4, no code edited): the fix cannot live in TransferCredentialLookup.cs alone. `ForRedirectHops` returns a selector that REPLACES RedirectFollower's origin rules (a null from it means "no credentials", not "fall back"), and it receives only the hop URL. So under -u it must also know the first URL's host and whether --location-trusted is set, to keep -u for a same-host hop and for trusted hops while sending the hop's netrc entry otherwise. Plan: add optional `firstUrl` and `locationTrusted` parameters to `ForRedirectHops`; for -u with a netrc option return a selector that gives the hop host's netrc entry when found, else -u when trusted or same host, else nothing; pass both from CurlCommandRunner.cs:4206 and ManagedEasyTransfer.Preparation.cs:119. Added both files to touches; BL-5016 holds CurlCommandRunner.cs, so depends-on BL-5016.

- 2026-11-12 (lane 1): Done. `ForRedirectHops` takes the first URL; under a `-u` user name and a netrc option a hop `-u` goes to (same scheme, host and port, or any under `--location-trusted`) keeps `-u`, and any other hop is looked up as if `-u` were absent (its netrc entry, else its URL credentials, else none). Choice (ADR-1264): under `--location-trusted` `-u` wins on every hop, matching the first hop's measured precedence; not measured, so it keeps what Curl did before. The runner now builds its follower after the URL parse to pass it. Real curl was not measured again here: the review's REF result is pinned in TransferCredentialLookupRedirectNetrcTests, which uses no path or OS-specific text (platform-neutral). Quality: Curl.Engine.UnitLibrary and Curl.Console have 0 failing members.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: 5b0255c6 f9a85a99 a0b49a29.
- 2026-11-12: Doing -> Backlog. Needs Curl.Console/CurlCommandRunner.cs, held by BL-5016 (depends-on added)
- 2026-11-12: Backlog -> Doing. Criteria at claim: 5b0255c6 f9a85a99 a0b49a29.
- 2026-11-12: Doing -> Done. A redirect hop under -u and a netrc option sends its own host's netrc entry
