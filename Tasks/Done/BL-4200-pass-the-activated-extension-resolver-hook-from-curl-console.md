---
id: BL-4200
title: Pass the activated extension resolver hook from Curl.Console to the engine
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4096, BL-4192]
touches: [Curl.Console/CurlComposition.cs, Curl.Console.UnitTests/ExtensionResolverHookTests.cs, Curl.Extension.Composition.UnitLibrary/ExtensionResolverHookCatalogue.cs, Tasks/SharedListFiles.txt, Curl.Extension.Composition.UnitLibrary/CLAUDE.md, .gitattributes]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4200 — Pass the activated extension resolver hook from Curl.Console to the engine

## Goal

Giving an extension's resolver option on the curl command line makes that extension's `IResolverHook` answer the transfer's host lookups, and giving it beside `--doh-url` (or beside a second resolver option) fails as a command-line error naming both.

## Context

BL-4096 added `ResolverHooks` in `Curl.Extension.Abstractions/Resolution` and the engine seam `extensionResolver` on `EngineComposition.CreateProtocolHandlers` (ADR-1012). No host passes it yet. Mirror BL-4192 for connection hooks: a one-line-per-extension catalogue of resolver-hook activators in `Curl.Extension.Composition`, `ResolverHooks.ActivatedBy` over the option group's `ExtensionOptionValues` with whether `--doh-url` was given, and `ResolverOver` passed in `CurlComposition`. The `ArgumentException` becomes curl's option error (exit 2) with its message. The connectors resolve through `CurlTransports`' resolver, not the handlers' `dnsResolver`, so the hook must also go under the TCP and UDP connectors' look-ups (as `wrapLookUps` puts `ResolverStartDnsResolver` there in `EngineComposition.CreateTransports`); if that needs an engine change, add the engine file to touches.

## Acceptance criteria

- [x] With a test extension's resolver option given, a Console transfer's host is looked up with that extension's resolver (unit test, no network).
- [x] With a resolver-hook option and `--doh-url` given, curl exits 2 and the message names both options (unit test).
- [x] Without an extension option, the resolver Console builds is unchanged (unit test).
- [x] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes
- Plan (mirrors BL-4192): `ExtensionResolverHookCatalogue` (empty, one entry per line, `merge=union`, shared list file); `CurlComposition.ExtensionResolverHooksOf(options[, activators])` calls `ResolverHooks.ActivatedBy` with `options.DohUrl is not null` and turns its `ArgumentException` into `ExtensionOptionConflictException` (exit 2 with the message; the shared `OptionConflictOf` now serves both hook kinds); `ExtensionResolverOf` gives the engine's `extensionResolver` (null without a hook).
- Connectors: no engine change was needed. `Curl.Console` (assembly `curl`) already sees the engine's internal `CreateTransports` overload with `wrapLookUps`, so the executable's new `CreateNetworkTransferDispatch` passes `ResolverHooks.ResolverOver` there (TCP and UDP connectors' look-ups) and to `CreateTransferDispatch(CurlTransports, ...)`'s new `extensionResolverHooks` (handlers' `dnsResolver`). The dialing runner wraps its resolver with the hook before the group's `TcpConnector` and handlers are built, and takes `extensionOptions` and `extensionResolverHookActivators` as test seams.
- Choice: the scripted-connector `CreateRunner` (tests only, no resolver of its own beyond `SystemDnsResolver`) takes no resolver activators; the acceptance tests drive the dialing runner, whose `TcpConnector` really looks the host up, so they prove the connectors' look-up goes through the hook (no network: a recording resolver and a refusing dialer).
- Touches widened by `Curl.Extension.Composition.UnitLibrary/CLAUDE.md` (its catalogue list must stay true) and `.gitattributes` (the `merge=union` line); no task in Doing on origin/work/dark-factory names either.
- No new ADR: the design is ADR-1012's, as the task's Context set it.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Console,Curl.Extension.Composition.UnitLibrary`: 0 failing members. `dotnet build` clean (0 warnings); whole-solution fast tests green on Windows. The tests use no paths, OS text or certificates, so they are platform-neutral.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: bd441ce9 a3da5757 d406d9b7 478f867b 3d836d5e.
- 2026-10-29: Doing -> Done. An extension's resolver option answers a Console transfer's host look-ups; beside --doh-url or another resolver option it exits 2 naming both
