---
id: BL-5168
title: Export the OpenTelemetry span of a watchdog-stopped chain with a token the watchdog has not cancelled
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.OpenTelemetry.UnitLibrary/OpenTelemetryTransferHook.cs, Curl.Extension.OpenTelemetry.UnitTests/OpenTelemetryChainEndTests.cs]
requirement: none
created: 2026-11-13
completed:
---
# BL-5168 — Export the OpenTelemetry span of a watchdog-stopped chain with a token the watchdog has not cancelled

## Goal

When the -m or -Y/-y watchdog stops a followed redirect chain, the OpenTelemetry hook still exports the chain's span, or writes its "OTLP export ... failed" warning, instead of losing it silently.

## Context

Found by the shift-end review of 7747c1d4d...8f429f2ab (silent-failure-hunter, type-design-analyzer and code-reviewer all reported it). BL-5165 made TransferPerformer.FinishedByWatchdogAsync (Curl.Engine.UnitLibrary/TransferPerformer.cs:188-203) tell hooks the chain finished after a watchdog fired. But the context it passes carries the watchdog's token: TransferContextFactory.cs:301 builds CancellationToken from TokenOf(abortToken, lowSpeedWatchdog, maxTimeWatchdog), and the watchdog has already cancelled it. OpenTelemetryTransferHook.TransferChainFinishedAsync (Curl.Extension.OpenTelemetry.UnitLibrary/OpenTelemetryTransferHook.cs:60-66) exports through ExportAsync (:69-77), which passes chain.LastHop.CancellationToken to OtlpHttpExporter.ExportAsync. ConnectAsync/WriteAsync then throw OperationCanceledException, which the exporter does not catch (it catches IOException), and TransferHookingProtocolHandler's new catch (Exception) swallows it. Scenario: curl -L -m 2 --otel <endpoint> http://a/ where hop 1 answers 302 and hop 2 stalls: exit 28, no span, no warning. The new tests miss it because their handlers ignore the token and OpenTelemetryChainEndTests has no cancelled-token case. The export at chain end should use a token not linked to the transfer's watchdog (for example CancellationToken.None bounded by its own short timeout through the context's TimeProvider).

## Acceptance criteria

- [ ] OpenTelemetryTransferHook.TransferChainFinishedAsync exports with a token that the transfer's watchdog cannot have cancelled, and that export is still bounded in time.
- [ ] A test in Curl.Extension.OpenTelemetry.UnitTests calls TransferChainFinishedAsync for a stored chain whose context's CancellationToken is already cancelled and asserts exactly one span reaches the exporter.
- [ ] Curl's output bytes and exit code for the transfer are unchanged (still exit 28).
- [ ] dotnet build is clean and the fast tests pass; Curl.Extension.OpenTelemetry.UnitLibrary keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-13: Created.
