---
id: BL-2090
title: Do not send LOGIN with an empty password after an HTTP-to-IMAP redirect (upstream test779)
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Protocol.Abstractions.UnitLibrary]
requirement: none
created: 2026-10-11
completed: 2026-10-11
---
# BL-2090 — Do not send LOGIN with an empty password after an HTTP-to-IMAP redirect (upstream test779)

## Goal

After an HTTP-to-IMAP redirect Curl sends nothing after `B001 CAPABILITY` and exits 67, as curl 8.21.0 does, instead of sending `B002 LOGIN v ` with an empty password, so upstream test779 passes.

## Context

In upstream test779 an HTTP response redirects (`--location` with `--oauth2-bearer`) to an IMAP URL; curl sends no login and exits 67 (login denied). Curl sends `B002 LOGIN v ` with an empty password. The cause is UNCONFIRMED; suspects: `ImapAuthentication.MayLogIn` (Curl.Protocol.Imap.UnitLibrary/ImapAuthentication.cs near line 234) and the redirect dropping the --oauth2-bearer token at Curl.Core.UnitLibrary/RedirectFollower.cs near line 594. Confirm the cause first with a failing unit test, then fix the right place. Read 8.21.0 `tests/data/test779` and measure real curl with `Record-CurlExchange.ps1` to pin the bytes.

Source: re-measurement of the gap findings with the fixed gap harness (PR #119, 2026-10-11); parent re-close task BL-2080. Targeted curl version: 8.21.0 (tests/data in the release tarball).

## Acceptance criteria

- [x] The confirmed cause (which suspect, or another) is written in the task Notes, backed by a failing-then-passing MSTest.
- [x] A new MSTest (Curl.Protocol.Imap.UnitTests and/or Curl.Core.UnitTests, no network) shows the IMAP session sends no command after CAPABILITY when no usable credential exists and the transfer returns `CurlExitCode` 67, with the bytes test779 expects.
- [x] Adding test779 to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is filed as BL-2094 (that project was held by BL-2097 in Doing).
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.

## Notes

- Cause (both suspects, neither alone): `RedirectFollower.NextHop` built the hop's context without `Mail`, so the IMAP hop lost every mail option, and `ImapAuthentication.MayLogIn` then fell back to `LOGIN v ` with the URL's user and empty password. curl 8.21.0 withholds the bearer from another host, but `--oauth2-bearer` also sets `CURLAUTH_BEARER`, so `Curl_sasl_init` prefers only OAUTHBEARER/XOAUTH2 and imap's `preftype` becomes SASL: no LOGIN fallback, `Curl_sasl_is_blocked`, exit 67.
- Fix: `RedirectFollower.HopMail` carries `Mail` to every hop and, when the hop may not be sent credentials, clears `BearerToken` and sets the new `MailRequestOptions.BearerTokenWithheld`; `MayLogIn` refuses LOGIN when it is set and no `AUTH=` option names mechanisms. Tests: `ImapProtocolHandlerAuthenticationTests.ExecuteAsync_BearerTokenWithheldByRedirect_SendsNoLoginUnlessOptionsNameMechanisms` (failed before: sent `A002 LOGIN v `), `RedirectFollowerTests.FollowAsync_MailBearerToken_IsWithheldFromAnotherHost` and two siblings.
- Added `Curl.Protocol.Abstractions.UnitLibrary` to `touches` for the new `MailRequestOptions` property; no task in Doing named it.
- Choice: kept the no-LOGIN rule to the withheld case only; whether a present `--oauth2-bearer` also blocks LOGIN on a direct IMAP URL was not re-measured here (the existing `bearer-none-offered` test already pins exit 67 there).
- Not measured: Record-CurlExchange.ps1 was not run; bytes pinned from 8.21.0 `tests/data/test779` (`B001 CAPABILITY` only, exit 67).

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
- 2026-10-11: Doing -> Done. HTTP-to-IMAP redirect to another host withholds the bearer and sends no LOGIN; exit 67 as test779
