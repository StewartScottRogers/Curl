# Curl.Conformance.UnitProgram.UnitLibrary

C# ports of upstream curl 8.22.0's unit programs - `tests/unit/unit<n>` and `tests/tunit/tool<n>` -
which the upstream case runner runs for a case with the `unittest` feature (ADR-0711). Namespace
`Curl.Conformance.UnitProgram`.

## Shape

- `IUnitProgram` is one program: `ProgramName` (`unit1302`, `tool1394`) and `RunAsync(context, token)`,
  the program's `test()`.
- `UnitProgramContext` is what the program gets: `TestNumber`, `Arguments` (the case's command words,
  copied, possibly none), and, set by the case runner, its `StandardOutput` and `StandardError`.
- `UnitProgramResult` is `unitcheck.h`'s `unitfail`: `FailureCount`, which is the `ExitCode`; `Success` is 0.
- `UnitPrograms` is the table of ported programs by `ProgramName` the case runner looks a case's program
  up in: an explicit list, never a reflection scan (AOT). Two programs of one name throw.
  `UnitPrograms.Ported` is built from one file per area, `UnitPrograms.<Area>.cs` (Hashes, Asn1, UrlParsing, ToolParsers, Headers, Dns, Limits); each port task (BL-2842 to BL-2848) appends its program to its own area's file only. The csproj already references every library the ports run against.

The case runner (`Curl.Conformance.UnitLibrary`) names a `unittest` case's program from its
`<client><tool>` (`tool%TESTNUMBER`, `unit1305`) or, with no `<tool>`, as `unit%TESTNUMBER`, and skips the
case with `Curl has no port of <program>` when the table lacks it.

## Rules

- Unlike `Curl.Conformance.LibTest.UnitLibrary`, a port may reference the library it tests (`Curl.Core`,
  `Curl.Cryptography`, `Curl.Cli`, `Curl.Networking`, `Curl.Protocol.*`, `Libcurl` and the like), and reach
  an `internal` type through that library's `InternalsVisibleTo`; never `Curl.Conformance.UnitLibrary`,
  which references this library, not the other way round.
- Held to the `*.UnitLibrary` gates: 100% line and branch coverage, complexity at most 10.
