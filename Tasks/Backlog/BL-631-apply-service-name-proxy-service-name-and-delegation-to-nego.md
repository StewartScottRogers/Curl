---
id: BL-631
title: Apply --service-name, --proxy-service-name and --delegation to Negotiate and GSS-API
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-630, BL-527]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-631 — Apply --service-name, --proxy-service-name and --delegation to Negotiate and GSS-API

## Goal

The Negotiate authenticator (BL-527) and the GSS-API users (SASL GSSAPI BL-538, SOCKS5 GSS-API BL-615, when they land) build the service principal name from `--service-name` (server) and `--proxy-service-name` (proxy) instead of the default `HTTP`/`smtp`/`rcmd`, and request credential delegation per `--delegation`, as curl 8.21.0 does.

## Context

- Conformance audit 2026-09-28, row 23 (Major). Options: BL-630; token seam: BL-525's ADR, BL-527.
- `System.Net.Security.NegotiateAuthenticationClientOptions` has `TargetName` and `RequiredProtectionLevel`/`AllowedImpersonationLevel` for delegation; unit tests use the fake token source and assert the options passed.
- The SPN format per scheme (`HTTP@host` vs `HTTP/host`) must match what curl passes; record it from curl's documentation and the `-v` output of a failing `--negotiate` run.

## Acceptance criteria

- [ ] `Curl.Authentication.UnitTests` pin the target name and delegation level passed to the token source for defaults and each option, for HTTP and proxy.
- [ ] A `Curl.Console.UnitTests` test shows each option reaching the authenticator.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
