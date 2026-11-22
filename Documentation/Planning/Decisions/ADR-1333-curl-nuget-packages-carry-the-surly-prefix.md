# ADR-1333: Curl's NuGet packages carry the `Surly.` prefix

- Status: Accepted (2026-11-21, BL-5707)
- Decided by Stewart; recorded by Claude.

## Context

nuget.org holds native-libcurl wrappers, native binaries and curl-command converters under generic
names. The bare IDs `Curl.Api`, `Libcurl` and `Curl.Mcp` do not tell Stewart's managed
reimplementation apart from them. Stewart will ask nuget.org to reserve the `Surly.` prefix for his
account, so only he can publish under it and the packages show the verified tick.

## Decision

| Package | `PackageId` |
| --- | --- |
| Curl.Api | `Surly.Curl.Api` |
| Libcurl | `Surly.Libcurl` |
| Curl.Mcp tool (command `curl-mcp`) | `Surly.Curl.Mcp` |
| Curl.Mcp runtime packages | `Surly.Curl.Mcp.<rid>` |

Only `PackageId` changes (and what NuGet ties to it: Libcurl's `build/Libcurl.props` became
`build/Surly.Libcurl.props`, as NuGet wants `build/<PackageId>.props`; the MCP `server.json`
`identifier`; the release file-name checks and the release resources listing).

Namespaces (`Curl.Api`, `Libcurl`, `Curl.Mcp...`), assembly names, project folders and the commands
`curl`, `curl-config` and `curl-mcp` do not change: a consumer keeps `using Curl.Api;` and
`using Libcurl;`, and a rename of them would break every caller and the drop-in promise for no gain,
since the package ID alone tells the packages apart on nuget.org.
