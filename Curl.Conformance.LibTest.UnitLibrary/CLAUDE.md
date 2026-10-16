# Curl.Conformance.LibTest.UnitLibrary

C# ports of upstream curl 8.22.0's `tests/libtest` programs, written over `Libcurl`, which the
upstream case runner runs for a case whose `<client><tool>` line names `lib%TESTNUMBER` or
`libNNN` (phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md`, ADR-0506). Namespace
`Curl.Conformance.LibTest`.

## Shape

- `ILibTest` is one program: `ProgramName` (such as `lib500`) and `RunAsync(context, token)`,
  libtest's `test()`.
- `LibTestContext` is what `first.c` hands `test()`: `TestNumber` (`testnum`), `Arguments` (after
  the program name, copied), `Url` (`argv[1]`) and `Argument2` to `Argument4`
  (`libtest_arg2` to `_arg4`, `null` when not given).
- `LibTestResult` is the exit code `main()` returns: `Success` (0), or `FromCurlCode` by number.
- `LibTestErrorCodes` is `first.h`'s `TEST_ERR_*` codes, 8.22.0's `CURLE_OBSOLETE*` values (ADR-0653).
- `LibTestSteps` is `first.h`'s step macros carrying `result` as `Result`: `GlobalInit`,
  `EasyInit`, `EasySetOption` (`easy_setopt`), `EasyPerformAsync`, `EasyGetInfo` and
  `SlistAppend`. Each answers whether the program goes on; on `false` the port goes to its
  cleanup and returns `ToLibTestResult()`.

## Rules

- Reference `Libcurl` and `Curl.Protocol.Abstractions.UnitLibrary` only; never a protocol library,
  `Curl.Api` or `Curl.Conformance.UnitLibrary` (the case runner references this library, not the
  other way round).
- Held to the `*.UnitLibrary` gates: 100% line and branch coverage, complexity at most 10.
