---
id: BL-4200
title: Pass the activated extension resolver hook from Curl.Console to the engine
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4096, BL-4192]
touches: [Curl.Console/CurlComposition.cs, Curl.Console.UnitTests/ExtensionResolverHookTests.cs, Curl.Extension.Composition.UnitLibrary/ExtensionResolverHookCatalogue.cs, Tasks/SharedListFiles.txt]
requirement: none
created: 2026-10-29
completed:
---
# BL-4200 — Pass the activated extension resolver hook from Curl.Console to the engine

## Goal

Giving an extension's resolver option on the curl command line makes that extension's `IResolverHook` answer the transfer's host lookups, and giving it beside `--doh-url` (or beside a second resolver option) fails as a command-line error naming both.

## Context

BL-4096 added `ResolverHooks` in `Curl.Extension.Abstractions/Resolution` and the engine seam `extensionResolver` on `EngineComposition.CreateProtocolHandlers` (ADR-1011). No host passes it yet. Mirror BL-4192 for connection hooks: a one-line-per-extension catalogue of resolver-hook activators in `Curl.Extension.Composition`, `ResolverHooks.ActivatedBy` over the option group's `ExtensionOptionValues` with whether `--doh-url` was given, and `ResolverOver` passed in `CurlComposition`. The `ArgumentException` becomes curl's option error (exit 2) with its message. The connectors resolve through `CurlTransports`' resolver, not the handlers' `dnsResolver`, so the hook must also go under the TCP and UDP connectors' look-ups (as `wrapLookUps` puts `ResolverStartDnsResolver` there in `EngineComposition.CreateTransports`); if that needs an engine change, add the engine file to touches.

## Acceptance criteria

- [ ] With a test extension's resolver option given, a Console transfer's host is looked up with that extension's resolver (unit test, no network).
- [ ] With a resolver-hook option and `--doh-url` given, curl exits 2 and the message names both options (unit test).
- [ ] Without an extension option, the resolver Console builds is unchanged (unit test).
- [ ] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
