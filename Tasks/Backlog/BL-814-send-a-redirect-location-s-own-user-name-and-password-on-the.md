---
id: BL-814
title: Send a redirect Location's own user name and password on the next hop
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-791]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-814 — Send a redirect Location's own user name and password on the next hop

## Goal

Under `-L`, a `Location` that carries user information (`Location: http://c:d@127.0.0.1:<P>/b`) makes the next hop send that user name and password, as curl 8.21.0 does, instead of the first URL's credentials or none.

## Context

- Found in BL-791: `Curl.Console/TransferCredentialLookup.cs` turns the first URL's user information into `ITransferContext.Credentials` once per URL; `Curl.Core.UnitLibrary/RedirectFollower.cs` then keeps those credentials to the same host and drops them to another (`sendCredentials`), and never reads the hop URL's own `User`/`Password`.
- Related: BL-790 looks the netrc entry up again for each hop's host; keep the two consistent.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1` (two connections): a 302 to `/b` on the same host with and without user information in `Location`, and to another host (`localhost`) with user information, for `http://a:b@` and for `-u q:r`; request bytes copied into Notes.
- [ ] `Curl.Core.UnitTests` pin each measured case.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Core` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
