---
id: BL-4341
title: gRPC extension: unary calls over grpc:// and grpcs:// with -d JSON and a .proto file
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4340]
touches: [Curl.Extension.Grpc.UnitLibrary/Client, Curl.Extension.Grpc.UnitTests/Client, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4341 — gRPC extension: unary calls over grpc:// and grpcs:// with -d JSON and a .proto file


## Goal

`curl grpc://host:port/pkg.Service/Method --proto file.proto -d '{json}'` (and `grpcs://` over TLS) makes a unary gRPC call over HTTP/2 and writes the response message as proto3 JSON.

## Context

Second of three parts of BL-4102, on BL-4340's `Wire/`. Specification: grpc.io PROTOCOL-HTTP2.md. Pattern: `Curl.Extension.Coap.UnitLibrary/Client/CoapExtension.cs` (description and `CreateHandler(ProtocolHandlerServices)`, registered by one line in `ExtensionCatalogue.cs`); HTTP/2 via `Curl.Http2.UnitLibrary`'s `Http2Connection`, TLS with ALPN `h2` via `services.TlsProvider`; descriptors and JSON via `Curl.Protobuf.UnitLibrary` (`ProtoFileSetReader`, `ProtobufJsonConverter`). The option (`--proto`) is declared in one file and added by one line to `ExtensionOptionCatalogue.cs` (ADR-1006); the description by one line to `ExtensionDescriptionCatalogue.cs`. Leave the no-`--proto` path (server reflection) to BL-4342: until then it fails with a clear message.

## Acceptance criteria

- [ ] `grpc://` and `grpcs://` are registered by one line in `ExtensionCatalogue.cs`, and the description appears in `curl --extensions` and `curl --ai-help extensions`.
- [ ] A unary call encodes `-d` JSON by the `--proto` file's method input type, sends it over HTTP/2 (h2c prior knowledge for `grpc://`, TLS with ALPN `h2` for `grpcs://`), and writes the output message as JSON; unit tests over a fake connection with recorded bytes, naming PROTOCOL-HTTP2.md sections.
- [ ] A non-zero `grpc-status` ends the transfer with a non-zero curl exit code and `grpc-message` on stderr; the mapping is recorded in the ADR; unit tests.
- [ ] With `CURL_EXTENSIONS=0` a `grpc://` URL fails exactly as curl fails an unsupported scheme (exit 1, same stderr bytes); unit test.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records the scheme and option names, the URL path form, the exit-code mapping and the specification version followed.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 12ee8bbf 90d861e8 73389f94 62009cc1 2881ec96 e35aff77 3d836d5e.
