---
id: BL-629
title: Parse --aws-sigv4 and sign HTTP requests with it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-628]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-629 — Parse --aws-sigv4 and sign HTTP requests with it

## Goal

`--aws-sigv4 <provider-spec>` with `-u key:secret` makes every HTTP request of the transfer carry the headers BL-628's signer produces, in the header positions curl 8.21.0 uses, overriding Basic as curl does.

## Context

- Conformance audit 2026-09-28, row 22 (Major). Signer: BL-628.
- Parse in `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs`; the HTTP authenticator is composed in `Curl.Console` (`IHttpAuthenticator`, ADR-0014); header order rules are FR-066 and ADR-0022. Decide in the XML docs whether signing happens in an authenticator or in the request options, choosing whichever lets the signer see the final headers and body.
- Use BL-628's measurements; add a case with `-H` custom headers and one with a redirect (`-L`) if curl re-signs.

## Acceptance criteria

- [ ] `Curl.Cli.UnitTests` cover parsing; `Curl.Console.UnitTests` pin the request bytes for BL-628's measured cases (fixed time) and the extra cases above, measured first with `Record-CurlExchange.ps1`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
