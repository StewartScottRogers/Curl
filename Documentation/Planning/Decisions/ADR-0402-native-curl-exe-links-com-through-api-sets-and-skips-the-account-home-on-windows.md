# ADR-0402 — The native `curl.exe` links COM through the API sets and skips the account home lookup on Windows

- **Status:** Accepted
- **Date:** 2026-10-02
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

BL-1288 found the published native `curl.exe` loading `ole32.dll`, `USER32.dll`, `GDI32.dll`,
`gdi32full.dll`, `win32u.dll` and `IMM32.DLL` on a plain `http://` GET: about 1.1 MB of working set
real `curl.exe` never maps. Two causes, found in BL-1290:

1. ILC links the runtime's seven COM calls (`CoCreateGuid`, `CoGetApartmentType`, `CoInitializeEx`,
   `CoTaskMemAlloc`, `CoTaskMemFree`, `CoUninitialize`, `CoWaitForMultipleHandles`) against
   `ole32.lib`, and `ole32.dll` statically imports `USER32.dll` and `GDI32.dll`. ILC's
   `SetupOSSpecificProps` target turns `@(SdkNativeLibrary)` into `@(LinkerArg)`, which `LinkNative`
   writes to `link.rsp`; editing `NativeLibrary` or `SdkNativeLibrary` later changes nothing.
2. `Environment.GetFolderPath(SpecialFolder.UserProfile)` calls `SHGetKnownFolderPath`, which loads
   `shell32.dll`, and `shell32.dll` loads `USER32.dll`. Curl called it at start-up for the last
   `.curlrc` directory and the last `known_hosts` directory, both of which curl searches only off
   Windows.

## Decision

1. `Curl.Console.csproj` has a target, `LinkComThroughApiSetsNotOle32`, that runs after
   `SetupOSSpecificProps` for a `win-*` runtime identifier and replaces `"ole32.lib"` in
   `@(LinkerArg)` with `"mincore.lib"`. All seven functions then bind to
   `api-ms-win-core-com-l1-1-0.dll`, served by `combase.dll` without user32. Native AOT on .NET 10
   needs Windows 10, where the API sets exist. Off Windows the target does not run.
2. `Curl.Cli.AccountHomeDirectory.ForProcess` is `null` on Windows and the user-database home
   elsewhere; `DefaultConfigFileSearch.ForProcess` and `CurlComposition` take it. Nothing on Windows
   read the value, so no search order changes.

## Consequences

A loopback GET's process loads 22 modules instead of 34 (BL-1290 Notes). The link swap depends on
ILC's item names (`LinkerArg`, `SetupOSSpecificProps`); a future ILC that renames them would silently
link `ole32.lib` again, which the BL-1290 `dumpbin /imports` check would show.

## Alternatives considered

- `<NativeLibrary Include="mincore.lib" />`: links ahead of the runtime's own `.lib`, so
  `CoWaitForMultipleHandles` still came from `ole32.lib` (BL-1288).
- Removing `ole32.lib` from `@(NativeLibrary)` before `LinkNative`: the link line is already built.
- Reading `USERPROFILE` instead of the known folder on Windows: needless, since Windows never uses it.
