---
id: BL-4170
title: Parse proto3 messages and enums into Protocol Buffers descriptors in Curl.Protobuf
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4164]
touches: [Curl.Protobuf.UnitLibrary/Descriptors, Curl.Protobuf.UnitTests/Descriptors]
requirement: none
created: 2026-10-29
completed:
---
# BL-4170 — Parse proto3 messages and enums into Protocol Buffers descriptors in Curl.Protobuf

## Goal

`Curl.Protobuf.UnitLibrary` reads proto3 `.proto` text into run-time file, message, field and enum descriptors, and malformed text fails with a typed error naming line and column.

## Context

First of three parts split from BL-4165 (itself split from BL-4099) because the whole did not fit one run. Builds on BL-4164's project. Code under `Descriptors/` in the library, tests under `Descriptors/` in `Curl.Protobuf.UnitTests`. Base class library only, a hand-written tokenizer and recursive-descent parser, native AOT safe. Covers the proto3 language specification (https://protobuf.dev/reference/protobuf/proto3-spec/): comments, `syntax`, `package`, `option` (file, message, field and enum level, kept as name/value pairs), messages (nested), enums (including `allow_alias` and negative values), fields of every scalar type, message and enum type references, `repeated`, `optional`, `oneof`, `map<,>`, `reserved` (numbers, ranges, `max`, names). Give each top-level construct its own parse method so BL-4168 can add `service`/`rpc` and `import` without rewriting the parser. Syntax errors come back as a result carrying a typed error with line and column, never an exception escaping.

## Acceptance criteria

- [ ] Proto3 text's messages (nested), enums, fields (every scalar type, `repeated`, `optional`, `oneof`, `map<,>`), `reserved`, `syntax`, `package` and `option` are read into descriptors, with a unit test for each construct.
- [ ] Malformed `.proto` text fails with a typed error naming line and column, never an exception escaping; unit tests.
- [ ] `Curl.Protobuf.UnitLibrary` meets the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-29, lane 5, first claim: ran out of the run's $3 budget after writing the tokenizer
  (left uncommitted for the shift to stash; it builds clean, no tests yet):
  `Descriptors/ProtoTokenizer.cs` (identifiers, decimal/octal/hex integers and floats checked
  by `GeneratedRegex`, strings with `\a..\v \\ \' \" \? \xHH \ooo` escapes decoded into
  `Text`, `//` and `/* */` comments, 1-based line and column, `Start`/`End` offsets into the
  source), `ProtoToken.cs` (with `ProtoTokenKind`), `ProtoIntegerLiteral.cs` (value of an
  integer literal, false on 64-bit overflow), `ProtoSyntaxError.cs` (public record Line,
  Column, Message) and `ProtoSyntaxException.cs` (internal, carries the error up to
  `ProtoParser.TryParse`, which catches it, so none escapes).
- Design for the rest, decided so the next claim need not redo it:
  - Public `ProtoParser.TryParse(string text, [NotNullWhen(true)] out ProtoFileDescriptor? file,
    [NotNullWhen(false)] out ProtoSyntaxError? error)`: try { tokenize, parse } catch
    `ProtoSyntaxException` only.
  - Internal `ProtoTokenCursor(source, tokens)`: `Peek`, `Next` (never past End), `Previous`,
    `AtEnd`, `IsKeyword`, `TryConsume(text)`, `Expect(text)`, `Expect(kind, description)`,
    `ExpectFullIdentifier` (joined with '.'), `ExpectInteger(min, max, description)` (optional
    '-', range-checked, "out of range" error), `SourceFrom(firstToken)` (source substring),
    static `Keyword(token)` (Text for Identifier and Symbol, "" otherwise) and `Unexpected`/`At`
    error builders ("Expected X, found \"y\"." / "end of input").
  - Statement dispatch by `Dictionary<string, Action<cursor, body>>` keyed on `Keyword`, not
    string switches (no hash-switch branches for coverage; BL-4168 adds `import` and
    `service` entries to the file table). Tables: file (`package`, `option`, `message`,
    `enum`, `;`), message (`message`, `enum`, `option`, `oneof`, `reserved`, `;`, default
    field), enum (`option`, `reserved`, `;`, default value), oneof (`option`, `;`, default
    field with oneof name, no label).
  - `syntax = "proto3";` required first: protoc reads a file without it as proto2, which this
    parser does not read. `Syntax` property is "proto3". A second `package` is an error.
  - Descriptors keep names as written, no full names (type resolution is BL-4168's):
    `ProtoFileDescriptor(Syntax, Package ("" when absent), Options, Messages, Enums)`,
    `ProtoMessageDescriptor(Name, Fields, Oneofs, Messages, Enums, Options, ReservedRanges,
    ReservedNames)`, `ProtoFieldDescriptor(Name, Number, Label, Type, MapKey?, MapValue?,
    OneofName?, Options)`, `ProtoTypeReference(ProtoFieldType Type, string Name)` (scalar
    names by dictionary, else `Named`; leading '.' kept), `ProtoFieldLabel` (Singular,
    Optional, Repeated; a map field is Repeated, as in descriptor.proto),
    `ProtoOneofDescriptor(Name, Options)`, `ProtoEnumDescriptor(Name, Values, Options,
    ReservedRanges, ReservedNames)`, `ProtoEnumValueDescriptor(Name, Number, Options)`,
    `ProtoOption(Name, Value)` (both the source text as written: `(my.opt).x`, `"com.x"` with
    quotes, `-5`, a whole `{ ... }` aggregate), `ProtoReservedRange(Start, End)` inclusive.
  - Checks: field number 1..`ProtobufReader.MaximumFieldNumber`, duplicate field number in a
    message, map key type in a `HashSet` (integral, bool, string), map field with a label or
    in a oneof, enum with no values, first enum value not 0, duplicate enum number without
    `allow_alias = true` (checked at the enum's close, error at the duplicate's number),
    reserved `a to b` with b < a, `max` = field maximum (messages) or int.MaxValue (enums).
  - Option constant by first token kind: string (adjacent strings joined), full identifier,
    integer/float, `-`/`+` then integer, float, `inf` or `nan`, or `{` aggregate skipped by
    depth (End inside it is an error).

- 2026-10-29, lane 1, second claim ($2.40 run budget): wrote the parser and descriptors on the
  design above, uncommitted for the shift's stash; `dotnet build Curl.Protobuf.UnitLibrary` is
  clean, no tests yet. Files: `ProtoParser` (public `TryParse`), `ProtoTokenCursor`,
  `ProtoStatements` (generic `ParseBlock`/`ParseStatement` over keyword tables, `ParseEmpty`),
  `ProtoFileParser`, `ProtoMessageParser` (fields, labels, `map<,>`, oneof, field numbers),
  `ProtoEnumParser`, `ProtoOptionParser`, `ProtoReservedParser`, the bodies (`ProtoFileBody`,
  `ProtoReservedBody` base of `ProtoMessageBody`/`ProtoEnumBody`, `ProtoOneofBody`) and the
  public records (`ProtoFileDescriptor`, `ProtoMessageDescriptor`, `ProtoFieldDescriptor`,
  `ProtoFieldLabel`, `ProtoFieldType` with `Named` and `Map`, `ProtoTypeReference`,
  `ProtoOneofDescriptor`, `ProtoEnumDescriptor`, `ProtoEnumValueDescriptor`, `ProtoOption`,
  `ProtoReservedRange` as int Start/End). Choices beyond the design, none needing an ADR:
  field numbers 19000 to 19999 are rejected as protoc does; an empty oneof and a labelled
  field in a oneof are errors; a map field's type is `Map` named `map<k, v>`; out-of-range
  numbers report "Number N is out of range (min to max)." at the number (or its '-').
  Remaining: unit tests in `Curl.Protobuf.UnitTests/Descriptors` (one per construct and per
  error, tokenizer escapes, comments, numbers and errors among them) to 100% line and branch
  coverage, one Measure-CodeQuality.ps1 run for the library, the fast tests, commit.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 690e3591 ee2341fd 7ae6d7ff 3d836d5e.
- 2026-10-29: Doing -> Backlog. Run budget spent after the tokenizer (left uncommitted for the shift's stash); parser, descriptors and tests remain, design recorded under Notes
- 2026-10-29: Backlog -> Doing. Criteria at claim: 690e3591 ee2341fd 7ae6d7ff 3d836d5e.
- 2026-10-29: Doing -> Backlog. Run budget spent after the parser and descriptors (uncommitted for the shift's stash, builds clean); unit tests, quality run and commit remain, recorded under Notes
- 2026-10-29: Backlog -> Blocked. Stewart: dark factory cost cap: its runs of the last day already cost 1.39 of its 3.13 US dollar cost cap, so it was not run again; split the task; the automatic split failed: the push of the split was refused
- 2026-10-29: Blocked -> Backlog. Cost cap window passed: its runs of the last 24 hours now leave 1 US dollar or more of its cost cap, so the cost cap allows a fresh run
- 2026-10-29: Backlog -> Doing. Criteria at claim: 690e3591 ee2341fd 7ae6d7ff 3d836d5e.
