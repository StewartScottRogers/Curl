---
id: BL-3824
title: Curl aborts (exit 134) on Linux when an error line is written to a closed standard error (2>&-)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/Program.cs, Curl.Console/StandardErrorOpener.cs, Curl.Console/NativeInheritedFileDescriptor.cs, Curl.Console.UnitTests/StandardErrorOpenerTests.cs, Curl.Console.UnitTests/NativeInheritedFileDescriptorTests.cs, Curl.Console.UnitTests/Cgr115Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3824 — Curl aborts (exit 134) on Linux when an error line is written to a closed standard error (2>&-)

## Goal

On Linux, Curl run with standard error closed (`2>&-`) exits with the transfer's own code, as curl does, instead of aborting.

## Context

Found by BL-3537 (CGR-008) on 2026-10-26 in WSL: `curl -sS http://127.0.0.1:21199/ 2>&-` exits 7 with curl 8.18.0 (OpenSSL); Curl (a self-contained linux-x64 publish of `Curl.Console`) prints `Aborted` and exits 134, both before and after BL-3537's `StandardErrorFailureIgnoringStream` (ADR-0815), which ignores every `IOException` from standard error. `-s` alone (no line written) and `--version 2>&-` exit normally, so the abort comes from writing to descriptor 2. Likely cause: the .NET runtime reuses the free descriptor 2 for one of its own (curl's `main_checkfds` fills 0, 1 and 2 with `/dev/null` at start for the same reason), so `Console.OpenStandardError` dups that descriptor and the error line lands in a runtime pipe or eventfd. Start in `Curl.Console/Program.cs`: find out what descriptor 2 is when `Main` starts (`/proc/self/fd/2`, `fstat`) and write standard error to `Stream.Null` when it is not the standard error the shell gave.

## Acceptance criteria

- [x] On Linux, `curl -sS http://127.0.0.1:21199/ 2>&-; echo $?` gives 7 through Curl as through real curl, with the commands and both results recorded in Notes.
- [x] A focused unit test in `Curl.Console.UnitTests` fails before the fix and passes after it (ADR-0482).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Cause: the code had moved on since filing (ADR-0866 opens descriptor 2 directly off Windows), but the cause held: with 2 closed the .NET runtime takes descriptor 2 for one of its own pipes, Curl's error line goes into it and the runtime aborts. Fix: `StandardErrorOpener` opens descriptor 2 only when `NativeInheritedFileDescriptor.IsInherited(2)` (open, and `FD_CLOEXEC` clear through the runtime's `SystemNative_FcntlGetFD` shim; every runtime descriptor is close-on-exec, an inherited one cannot be), else `Stream.Null`, where curl's `main_checkfds` `/dev/null` sends it. Decided in ADR-0869 (0867 was taken on the shared branch). `Program.cs` needed no change.
- Touches widened to `StandardErrorOpener.cs`, the new `NativeInheritedFileDescriptor.cs` and its tests, `StandardErrorOpenerTests.cs` and `Cgr115Tests.cs` (the opener's signature gained the descriptor check): the fix lives in the opener, not `Program.cs`; no task in Doing on origin/work/dark-factory names any of them.
- Measured in WSL Ubuntu with a self-contained linux-x64 publish (`dotnet publish Curl.Console -c Release -r linux-x64 --self-contained -p:PublishAot=false`), output to a file via `exec > /tmp/bl3824.out`: `curl -sS http://127.0.0.1:21199/ 2>&-; echo "real=$?"` gives `real=7` (curl 8.18.0, OpenSSL 3.5.5); Curl before the fix `ours=134` (Aborted), after the fix `ours=7`. `2>/dev/null`, `2>&1`, `> file 2>&1` and `2>&1 | cat` still give 7 and still write the line (dup2 clears close-on-exec).
- Focused tests: `StandardErrorOpenerTests.Open_OffWindowsDescriptor2NotInherited_OpensTheNullStreamNotTheDescriptor` (all platforms; does not compile, so fails, before the fix) and `NativeInheritedFileDescriptorTests` (off Windows only; no dotnet in WSL here, so they run on CI's Linux and macOS jobs). Platform-neutral: the Windows path is unchanged and the native tests carry OSCondition.
- `dotnet build` 0 warnings, 0 errors; fast tests all passed (Curl.Console.UnitTests 2977 passed, 31 skipped). `Measure-CodeQuality.ps1 -Library Curl.Console`: 0 failing members.
- Filed: BL-3936 (`1>&-` exits 2, curl 23) and BL-3937 (standard error into a shared file lands at the FileStream's own offset), both found while measuring.

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 8a2d5fed def504f7 779790c0.
- 2026-10-28: Doing -> Done. Curl run with standard error closed (2>&-) on Linux exits with the transfer's code (7) as curl does, instead of aborting with 134
