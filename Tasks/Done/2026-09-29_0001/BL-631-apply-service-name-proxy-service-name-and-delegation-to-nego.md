---
id: BL-631
title: Apply --service-name, --proxy-service-name and --delegation to Negotiate and GSS-API
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-630, BL-527]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-631 — Apply --service-name, --proxy-service-name and --delegation to Negotiate and GSS-API

## Goal

The Negotiate authenticator (BL-527) and the GSS-API users (SASL GSSAPI BL-538, SOCKS5 GSS-API BL-615, when they land) build the service principal name from `--service-name` (server) and `--proxy-service-name` (proxy) instead of the default `HTTP`/`smtp`/`rcmd`, and request credential delegation per `--delegation`, as curl 8.21.0 does.

## Context

- Conformance audit 2026-09-28, row 23 (Major). Options: BL-630; token seam: BL-525's ADR, BL-527.
- `System.Net.Security.NegotiateAuthenticationClientOptions` has `TargetName` and `RequiredProtectionLevel`/`AllowedImpersonationLevel` for delegation; unit tests use the fake token source and assert the options passed.
- The SPN format per scheme (`HTTP@host` vs `HTTP/host`) must match what curl passes; record it from curl's documentation and the `-v` output of a failing `--negotiate` run.

## Acceptance criteria

- [x] `Curl.Authentication.UnitTests` pin the target name and delegation level passed to the token source for defaults and each option, for HTTP and proxy.
- [x] A `Curl.Console.UnitTests` test shows each option reaching the authenticator.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- 2026-09-29, lane 3: delivered (ADR-0188, decided by Claude under Stewart's delegation).
  `NegotiateOptions` (Authentication) carries the two names and a `SecurityDelegation`;
  `NegotiateHttpAuthenticator` asks `ProxyServiceName ?? "HTTP"` for a proxy request and
  `ServiceName ?? "HTTP"` otherwise, with the delegation level. `Curl.Console`'s
  `NegotiateOptionsMapping` builds it per option group for the origin handler and the CONNECT
  tunnel's authenticator; `MailRequestOptions.ServiceName` now carries `--service-name` for SASL.
- SPN format: nothing to change. `SecurityContextRequest` keeps service and host apart; SSPI and
  the BCL GSS-API get `service/host` (the BCL converts to a host-based name off Windows), the
  hand-built route the principal `service/host@REALM`, which is what curl's `service@host`
  (GSS_C_NT_HOSTBASED_SERVICE) and SSPI `service/host` resolve to. Not measurable on the
  loopback recorder: it has no KDC, and curl prints the SPN nowhere in `-v` without one.
- Delegation, from curl's source: only `curl_gssapi.c` reads `CURLOPT_GSSAPI_DELEGATION`; the
  SSPI code never does. So the Windows router clears the level (overrides ADR-0142's "for W"),
  and off Windows `always` asks `TokenImpersonationLevel.Delegation`; `policy` asks none there
  because `NegotiateAuthentication` cannot ask `GSS_C_DELEG_POLICY_FLAG` and full delegation
  would forward the TGT to hosts not ok-as-delegate.
- Proxy Negotiate is still not answered (BL-604); the proxy name is pinned at the authenticator
  and takes effect when BL-604 lands. SASL GSSAPI is not wired into Console yet (BL-852), and
  SOCKS5 GSS-API (BL-615) is not built; both pick the names up from the same places.
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0188 and its index row; no task
  in Doing names it.
- Filed BL-873 (hand-built Kerberos delegation needs a forwarded TGT) and BL-874 (`--delegation`
  for SASL GSSAPI, needs `SaslRequest` in Abstractions).
- Tests: Authentication 604 passed (3 skipped), Console 1529 passed (9 skipped); Measure-CodeQuality:
  Curl.Authentication.UnitLibrary and Curl.Console 100% line, 100% branch, 0 failing, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Negotiate asks for --service-name / --proxy-service-name (else HTTP) with the --delegation level; SSPI never delegates (ADR-0188)
