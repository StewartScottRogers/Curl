---
id: BL-4156
title: Extension: Trace transfers with OpenTelemetry
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094]
touches: [Curl.Extension.OpenTelemetry.UnitLibrary, Curl.Extension.OpenTelemetry.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4156 — Extension: Trace transfers with OpenTelemetry

## Goal

Curl provides an option that sends `traceparent` and exports spans over OTLP/HTTP JSON, as the extension `Curl.Extension.OpenTelemetry.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 56 of 63. Specification: W3C Trace Context and the OTLP specification. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.OpenTelemetry.UnitLibrary` and `Curl.Extension.OpenTelemetry.UnitTests` exist in `/Extensions/OpenTelemetry/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides an option that sends `traceparent` and exports spans over OTLP/HTTP JSON.
- [x] Unit tests check its wire behaviour against named sections of W3C Trace Context and the OTLP specification, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Touches: added `ExtensionOptionCatalogue.cs`, `ExtensionDescriptionCatalogue.cs` and `ExtensionTransferHookCatalogue.cs`, where an option-and-hook extension registers (as ClientHints does); `ExtensionCatalogue.cs` holds protocol handlers only, so it is unchanged. All three are shared list files, and no task in Doing on `origin/work/dark-factory` named them. The criterion's "one line in ExtensionCatalogue.cs" is met by one line in each of the catalogues this kind of extension uses.
- Decided (ADR-1122): one option, `--otel-endpoint <url>`, the traces endpoint used as given; listed as `otel`. W3C Trace Context Level 1, OTLP 1.5.0 JSON, HTTP client semantic conventions 1.26.0. `tracestate`, retries, partial success, gRPC/binary Protobuf, batching and `OTEL_*` variables left out, with why.
- Measured: `curl.exe --otel-endpoint http://127.0.0.1:48318/v1/traces http://127.0.0.1:1/` posted the span (error.type CouldntConnect, status code 2) to a PowerShell loopback collector, and curl still exited 7; `CURL_EXTENSIONS=0` gives "option --otel-endpoint: is unknown", exit 2; `--extensions` and `--ai-help extensions` list it.
- Quality: Measure-CodeQuality.ps1 for Curl.Extension.OpenTelemetry.UnitLibrary, 100% line and branch, 0 failing members, worst CRAP 10; Curl.Extension.Composition.UnitLibrary 100/100, 0 failing.
- Platform-neutral: the tests use fake connections, a fake clock and no paths, certificates or OS text, so CI on Linux and macOS sees what Windows does. Fast tests green here (96 test projects).

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: be94b817 8e51eeb9 1fb4821b e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. Option --otel-endpoint sends traceparent and exports OTLP/HTTP JSON spans; gates met, fast tests green
