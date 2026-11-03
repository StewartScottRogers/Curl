---
id: BL-4787
title: Finish the ProtobufReader TryRead rename and make Curl.Protobuf CLAUDE.md and test names true
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protobuf.UnitLibrary, Curl.Protobuf.UnitTests, Curl.Extended.Protocol.Grpc.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
---
# BL-4787 — Finish the ProtobufReader TryRead rename and make Curl.Protobuf CLAUDE.md and test names true

## Goal

No `Curl.Protobuf` method that returns a `ProtobufDecodeError` is named `Try...`, and `Curl.Protobuf.UnitLibrary/CLAUDE.md` and the test names describe the code as it is.

## Context

Found by the shift-end review of 2026-11-03 (type-design-analyzer and code-reviewer, both Important), in the shift merged at 7410f102c.

- The shift renamed `TryConvert*`, `TryDecode` and `ReadPacked*` (AF-0610: "Try" methods that return an error, not a bool). It left nine public `ProtobufReader` methods with the same shape under `Try` names: `TryReadVarint`, `TryReadFixed32`, `TryReadFixed64`, `TryReadLengthDelimited`, `TryReadTag`, `TryReadEmbeddedMessage`, `TryReadField`, `TryReadFieldValue` and `TryReadAllFields` (`Curl.Protobuf.UnitLibrary/Wire/ProtobufReader.cs:42-187`). Four private helpers (~lines 262-307) keep the prefix too.
- The shift changed `Curl.Protobuf.UnitLibrary/CLAUDE.md:11` to "every read is a `Read...` returning a `ProtobufDecodeError`". That line is false for most of the type, and the same paragraph still names `TryReadField`.
- Test methods still carry the old names even though their bodies call the new ones: `Curl.Protobuf.UnitTests/Json/ProtobufJsonConverterTests.cs` (lines 12, 21, 30, 39, 48 and 60, `TryConvert*_...`) and `Curl.Protobuf.UnitTests/DynamicMessages/DynamicMessageDecoderTests.cs` (lines 31, 40 and 57, `TryDecode_...`).
- One concept, one name (CLAUDE.md, "Say what it does, do what it says").

## Acceptance criteria

- [ ] `grep -rn "ProtobufDecodeError Try" Curl.Protobuf.UnitLibrary` finds nothing. Every caller is updated, including `DynamicMessageDecoder.cs` and any users in the gRPC extension.
- [ ] No test method name in `Curl.Protobuf.UnitTests` starts with a `Try...` name of a method that no longer exists.
- [ ] `Curl.Protobuf.UnitLibrary/CLAUDE.md` names only methods that exist.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-03: Created.
