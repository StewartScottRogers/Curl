---
id: BL-4192
title: Pass the activated extension connection hook from Curl.Console to the engine
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4095, BL-4173]
touches: [Curl.Console/CurlComposition.cs, Curl.Console.UnitTests/ExtensionConnectionHookTests.cs, Curl.Extension.Composition.UnitLibrary/ExtensionConnectionHookCatalogue.cs, Tasks/SharedListFiles.txt]
requirement: none
created: 2026-10-29
completed:
---
# BL-4192 — Pass the activated extension connection hook from Curl.Console to the engine

## Goal

Giving an extension's option on the curl command line makes that extension's `IConnectionHook` carry the transfer, and two such options fail as a command-line error naming both.

## Context

BL-4095 added `ConnectionHooks` in `Curl.Extension.Abstractions/Connection` and the engine seam `extensionConnector` / `extensionDatagramConnector` on `EngineComposition.CreateProtocolHandlers` (ADR-1010). No host passes them yet. Mirror how BL-4173 wires the transfer hooks: a one-line-per-extension catalogue of connection-hook activators in `Curl.Extension.Composition`, `ConnectionHooks.ActivatedBy` over the option group's `ExtensionOptionValues`, and its `ConnectorOver` / `DatagramConnectorOver` passed in `CurlComposition`. The `ArgumentException` from two hooks becomes curl's option error (exit 2) with its message.

## Acceptance criteria

- [ ] With a test extension's option given, a Console transfer runs over that extension's connector (unit test, no network).
- [ ] With two connection-hook options given, curl exits 2 and the message names both options (unit test).
- [ ] Without an extension option, the connector Console builds is unchanged (unit test).
- [ ] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 5b7c3c09 ee312362 a8420816 478f867b 3d836d5e.
