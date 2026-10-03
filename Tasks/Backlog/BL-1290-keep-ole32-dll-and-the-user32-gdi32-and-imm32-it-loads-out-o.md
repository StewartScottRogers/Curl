---
id: BL-1290
title: Keep ole32.dll and the user32, gdi32 and imm32 it loads out of the native curl.exe
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1290 — Keep ole32.dll and the user32, gdi32 and imm32 it loads out of the native curl.exe

## Goal

The published native `curl.exe` on Windows imports none of its COM functions from `ole32.dll`, so a plain `http://` GET never loads `ole32.dll`, `USER32.dll`, `GDI32.dll`, `gdi32full.dll`, `win32u.dll` or `IMM32.DLL` (BL-1288).

## Context

Found in BL-1288 (its Notes) on 2026-10-02. `dumpbin /imports` of the published `curl.exe` shows
ILC binding seven COM functions to `ole32.dll`: `CoCreateGuid`, `CoGetApartmentType`, `CoInitializeEx`,
`CoTaskMemAlloc`, `CoTaskMemFree`, `CoUninitialize`, `CoWaitForMultipleHandles`. `ole32.dll` statically
imports `USER32.dll` and `GDI32.dll`, which bring `gdi32full.dll`, `win32u.dll`, `IMM32.DLL` and
`msvcp_win.dll`; real `curl.exe` loads none of them. Measured with `QueryWorkingSet` while the
transfer waits for the response, those modules hold about 1.1 MB of the working set (`combase.dll`
stays: it serves the COM API set). All seven functions are in the `api-ms-win-core-com-l1-1-*` API
sets, which `combase.dll` serves without user32.

Tried in BL-1288:
- `<NativeLibrary Include="mincore.lib" />` in an `ItemGroup` of `Curl.Console.csproj`: linked ahead of
  ILC's defaults, it bound six of the seven to `api-ms-win-core-com-l1-1-0.dll`, but
  `CoWaitForMultipleHandles` (referenced from the runtime's own `.lib`, which links after it) still
  came from `ole32.lib`, so `ole32.dll` still loaded. `mincore.lib` exports it, so this is link order.
- `onecore.lib` the same way: no change at all to the imports.
- A target with `BeforeTargets="LinkNative"` removing `ole32.lib` from `@(NativeLibrary)` and adding
  `mincore.lib`: no change to the imports, so the link step does not read `NativeLibrary` there; read
  `Microsoft.NETCore.Native.targets` (`LinkNative`, the `link.rsp` it writes) for the item or property
  it does read.
Publish with `C:\Program Files (x86)\Microsoft Visual Studio\Installer` on PATH, and delete
`Curl.Console/obj/Release/net10.0/win-x64/native` first or ILC does not relink. Native AOT on .NET 10
needs Windows 10 or later, where the API sets exist.

## Acceptance criteria

- [ ] `dumpbin /imports` of `dotnet publish Curl.Console -c Release`'s `curl.exe` lists no `ole32.dll`, and a 50 MiB loopback GET's process does not load `ole32.dll` or `USER32.dll` (listed through `Process.Modules` while the server holds the response), with the commands and output recorded under Notes.
- [ ] Off Windows the publish is unchanged (the change is conditioned on the Windows runtime identifier).
- [ ] No output byte, exit code or `-v` line changes: `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-02: Created.
