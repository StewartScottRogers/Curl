# ADR-1306 — The Libcurl package adds implicit usings for the Curl.Api namespaces a port needs

Status: Accepted. Decided by Claude under Stewart's delegation (BL-5551).

## Context
`CurlCode`, `CurlWebSocketFlags`, `RtspRequest` and the callback delegates live in `Curl.Api.*` namespaces,
so a C program ported to `Libcurl` needed four extra `using` lines.

## Decision
The `Libcurl` package ships `build/Surly.Libcurl.props`, which adds `<Using Include="..." />` items for
`Curl.Api.Callbacks`, `Curl.Api.Codes`, `Curl.Api.EngineContracts.WebSocket` and `Curl.Api.Options.Protocols`.
A consuming project sees those types with no edits. `Libcurl/README.md` lists the namespaces under "Porting from C",
and a test pins the README list to the props file.

## Why
Moving or forwarding the types is a breaking change to `Curl.Api`. A README list alone leaves every port to
copy the lines. The props file changes nothing for existing code and needs no new package.
