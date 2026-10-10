---
id: BL-2033
title: Send a Digest Authorization header in curl's Schannel-build form on Windows
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-2022]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-2033 — Send a Digest Authorization header in curl's Schannel-build form on Windows

## Goal

On Windows `curl --digest -u u:p -v` sends `Authorization: Digest username="u",realm="r",nonce="abc",uri="/64",response="fc3de222db74c3ec88aabb5510c76f80"`, byte for byte what curl 8.21.0's Schannel build sends, because `CurlComposition` builds its `DigestAuthenticator` with `matchesSspiBuild: OperatingSystem.IsWindows()`.

## Context

BL-2022 gave `Curl.Authentication.UnitLibrary`'s `DigestAuthenticator` a `matchesSspiBuild` constructor parameter that writes the value in the order and separators SSPI uses (measured, BL-2022 Notes): no blank after a comma, `algorithm` before `response`, `qop="auth"` quoted after it, `opaque` last. It could not wire it in `Curl.Console/CurlComposition.cs` (line ~245, `new DigestAuthenticator(credentialEncoding, DigestClientNonce.CreateRandom, diagnosticLog)`) because BL-1975 held Curl.Console. Pass `matchesSspiBuild: OperatingSystem.IsWindows()` there, as `NtlmHttpAuthenticator`'s `matchesSspiBuild` is passed. Check any `Curl.Console.UnitTests` or `Curl.Conformance.UnitTests` test that pins a Digest header on Windows (`grep -rn 'Digest username' Curl.Console.UnitTests`); if one in another project needs a change, widen `touches`.

Measure with `Record-CurlExchange.ps1 -OutDirectory <dir> -Port <p> -HalfCloseAfterResponse -HoldOpenMilliseconds 1500 -Connections 2 -Response '<401 with WWW-Authenticate: Digest realm="r", nonce="abc">','HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok' -CurlArgs '--digest','-u','u:p','-s','http://127.0.0.1:<p>/64'` (run it from the PowerShell tool, not Git Bash).

## Acceptance criteria

- [ ] On Windows the Digest `Authorization` value `Curl.Console` sends matches curl 8.21.0's bytes for the command above (measured, recorded under Notes).
- [ ] Off Windows the value keeps the `, ` form (no test on Windows changes it).
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green; Curl.Console keeps 100% line and branch coverage.
- [ ] No option changes, so `--ai-help` needs nothing; say so under Notes.

## Notes

## Log

- 2026-10-10: Created.
