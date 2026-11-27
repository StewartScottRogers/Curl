# ADR-1464: The curl program is published to nuget.org as the .NET tool Surly.Curl, native AOT per platform

- Status: Accepted
- Date: 2026-11-27
- Decided by Claude under Stewart's delegation (BL-6078)

## Context

The Surly book follows the nuget.org packages, not the repositories: every recipe must run for a reader who has only .NET and nuget.org (Stewart, 2026-11-27). Its console recipes need the curl program, but nuget.org held only `Surly.Curl.Mcp`, `Surly.Curl.Api` and `Surly.Libcurl`; the curl executable shipped only as GitHub release archives.

## Decision

`Curl.Console` packs as the .NET tool `Surly.Curl`, command name `curl`, in the shape of `Surly.Curl.Mcp` (ADR-0621):

- `dotnet pack Curl.Console` makes the tool package, which points at one package per runtime identifier: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64` and `osx-arm64`.
- `dotnet pack Curl.Console -r <rid>`, on that operating system, makes a runtime package holding the native-AOT `curl`. It is the binary the drop-in promise rests on, not a framework-dependent build.
- The command is `curl`, the program's name. `dotnet tool install -g Surly.Curl --prerelease` can shadow the system curl on PATH; `dnx Surly.Curl@<version> --yes -- <curl arguments>` runs it without installing. `Curl.Console/README.md`, the package's README, says both.
- The packages leave out the native symbols (about 150 MB unpacked on win-x64) and the XML documentation, which takes the win-x64 package from 43 MB to 15 MB.
- `release.yml`'s `pack-tool` job packs the seven packages, so a `v*` tag attaches and pushes 16 packages to nuget.org (ADR-1443 said nine). `resources.json` lists the new ones with the kind `dotnet-tool`.

## Consequences

- The six GitHub release archives stay as they were.
- Measured on Windows with .NET 10: the packed win-x64 `curl.exe` answers `--version` with `DOTNET_ROOT` naming no directory and no `dotnet` on PATH. Running it through `dnx` needs the .NET 10 SDK, which provides `dnx`.
- In PowerShell, `dnx` is a script, and PowerShell takes a bare `--` for itself, so `dnx` reads curl's `--version` as its own option. PowerShell users write `'--'`; the documents say so. `cmd` passes `--` as written.
- Each tag publishes seven more lasting versions to nuget.org.
