---
id: BL-4341
title: gRPC extension: unary calls over grpc:// and grpcs:// with -d JSON and a .proto file
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4340]
touches: [Curl.Extension.Grpc.UnitLibrary/Client, Curl.Extension.Grpc.UnitTests/Client, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Grpc.UnitLibrary/Curl.Extension.Grpc.UnitLibrary.csproj, Curl.Extension.Grpc.UnitLibrary/CLAUDE.md, Curl.Extension.Grpc.UnitTests/Curl.Extension.Grpc.UnitTests.csproj, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs, Curl.Console.UnitTests/CurlCompositionTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-30
---
# BL-4341 — gRPC extension: unary calls over grpc:// and grpcs:// with -d JSON and a .proto file


## Goal

`curl grpc://host:port/pkg.Service/Method --proto file.proto -d '{json}'` (and `grpcs://` over TLS) makes a unary gRPC call over HTTP/2 and writes the response message as proto3 JSON.

## Context

Second of three parts of BL-4102, on BL-4340's `Wire/`. Specification: grpc.io PROTOCOL-HTTP2.md. Pattern: `Curl.Extension.Coap.UnitLibrary/Client/CoapExtension.cs` (description and `CreateHandler(ProtocolHandlerServices)`, registered by one line in `ExtensionCatalogue.cs`); HTTP/2 via `Curl.Http2.UnitLibrary`'s `Http2Connection`, TLS with ALPN `h2` via `services.TlsProvider`; descriptors and JSON via `Curl.Protobuf.UnitLibrary` (`ProtoFileSetReader`, `ProtobufJsonConverter`). The option (`--proto`) is declared in one file and added by one line to `ExtensionOptionCatalogue.cs` (ADR-1006); the description by one line to `ExtensionDescriptionCatalogue.cs`. Leave the no-`--proto` path (server reflection) to BL-4342: until then it fails with a clear message.

## Acceptance criteria

- [x] `grpc://` and `grpcs://` are registered by one line in `ExtensionCatalogue.cs`, and the description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] A unary call encodes `-d` JSON by the `--proto` file's method input type, sends it over HTTP/2 (h2c prior knowledge for `grpc://`, TLS with ALPN `h2` for `grpcs://`), and writes the output message as JSON; unit tests over a fake connection with recorded bytes, naming PROTOCOL-HTTP2.md sections.
- [x] A non-zero `grpc-status` ends the transfer with a non-zero curl exit code and `grpc-message` on stderr; the mapping is recorded in the ADR; unit tests.
- [x] With `CURL_EXTENSIONS=0` a `grpc://` URL fails exactly as curl fails an unsupported scheme (exit 1, same stderr bytes); unit test.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records the scheme and option names, the URL path form, the exit-code mapping and the specification version followed.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Touches widened (no task in Doing on origin/work/dark-factory names them): the library csproj gains `Curl.Http2` and `Curl.Protobuf` references (its CLAUDE.md allows them for the client flow), the test csproj gains `Curl.Extension.Composition` and `Curl.Core` for the registration and `CURL_EXTENSIONS=0` tests (as the CoAP tests do), `CLAUDE.md` describes `Client/`, and `--proto` reaches the handler through a transfer hook registered by one line in `ExtensionTransferHookCatalogue.cs` (as `--coap-observe` does).
- `--proto` in the criteria is `--grpc-proto` on the command line: `--proto` is curl's own option (allowed protocols) and an extension never changes curl's behaviour (ADR-1145).
- ADR renumbered from 1138 to 1145: ADR-1138 and ADR-1139 were taken by other lanes first.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Extension.Grpc.UnitLibrary` gives 100% line, 100% branch, max complexity 8, 0 failing members. The Composition changes are one registration line per catalogue.
- CI on Linux and macOS: the tests use no paths, OS error text or certificates that differ by platform (proto files in a temporary directory, JSON written with LF line ends on every platform), and the fast tests are green here.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 12ee8bbf 90d861e8 73389f94 62009cc1 2881ec96 e35aff77 3d836d5e.
- 2026-10-30: Doing -> Backlog. Dark factory stopped at its 80-turn cap, so the task is too big for one run: split it in the first turns of the next claim; see Z:\repos\Curl.logs\BL-4341-20261029-184521-L1.jsonl
- 2026-10-30: Backlog -> Doing. Criteria at claim: 12ee8bbf 90d861e8 73389f94 62009cc1 2881ec96 e35aff77 3d836d5e.
- 2026-10-30: Doing -> Done. grpc:// and grpcs:// make unary gRPC calls over HTTP/2 with --grpc-proto and -d JSON
