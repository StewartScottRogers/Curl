# ADR-1332: The curl-mcp curl tool answers Digest as the platform's own curl does

Status: Accepted. Decided by Claude under Stewart's delegation (BL-5685).

## Context

`curl.exe` and the `curl-mcp` `curl` tool sent differently formatted `Authorization: Digest` headers. Both reach `DigestAuthenticator` through `EngineComposition`, but the tool runs `InProcessCurl.RunAsync`, which sets `usesHandBuiltNtlm: true` (the conformance harness's setting). `EngineComposition.MatchesSspiBuild` reads that as the non-SSPI build, so on Windows the tool wrote curl's own form (`, ` between parameters, base64 cnonce) while `curl.exe` wrote the Schannel build's SSPI form (no blanks, 32 hex digit cnonce), which curl 8.22.0's Schannel build sends.

## Decision

`InProcessCurl.RunAsync` over a TCP dialer takes `usesHandBuiltNtlm` (default `true`, so every conformance caller is unchanged). `InProcessCurlCommandRunner`, the tool's runner, passes `false`, so it composes the same authenticators as `curl.exe`: SSPI form on Windows, curl's own form elsewhere. NTLM follows the same rule.

## Consequences

Both hosts share one Digest path, `EngineComposition.CreateHttpAuthenticator`, with the same build matching. `InProcessCurlDigestBuildTests` pins both forms on Windows.
