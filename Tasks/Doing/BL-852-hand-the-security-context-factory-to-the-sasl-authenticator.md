---
id: BL-852
title: Hand the security context factory to the SASL authenticator in CurlComposition
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-538]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-852 — Hand the security context factory to the SASL authenticator in CurlComposition

## Goal

`curl smtp://…`, `imap://…` and `pop3://…` against a server offering only `AUTH NTLM` or `AUTH GSSAPI` authenticate through the production `RoutingSecurityContextFactory`, because `CurlComposition.CreateSaslAuthenticator` hands it to `SaslAuthenticator`.

## Context

- Follow-up of BL-538, which teaches `Curl.Authentication`'s `SaslAuthenticator` NTLM and GSSAPI over an `ISecurityContextFactory` but cannot edit `Curl.Console`. `CurlComposition.CreateSaslAuthenticator` (`Curl.Console/CurlComposition.cs`) builds `new SaslAuthenticator(CredentialEncoding.ForPlatform(...))` today; HTTP NTLM and Negotiate already get the router there.
- Measured SMTP NTLM exchange with curl 8.21.0 (Schannel) is in BL-538's Notes.

## Acceptance criteria

- [ ] A `Curl.Console.UnitTests` test runs an SMTP transfer against a server offering only `AUTH NTLM` with the fixed Type 2 challenge from BL-538's Notes and sees `AUTH NTLM`, then a Type 1 message, then a Type 3 message, then `MAIL FROM`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Lane 3 could not integrate: fast tests failed after rebasing onto the other lanes' work. The work is on branch factory/BL-852-lane-3-20260929-023709; start with git cherry-pick --no-commit factory/BL-852-lane-3-20260929-023709 and fix it.
- 2026-09-29: Backlog -> Doing.
