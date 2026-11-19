# ADR-1304: --cache-dir with an invalid path and --sigstore-bundle without its companions are refused at activation (BL-5488)

Decided by Claude under Stewart's delegation.

## Status

Accepted

## Context

Fitness finding EXT-094: `--cache-dir "<>:|bad"` printed `curl: (2) The filename, directory name, or volume label syntax is incorrect. : 'C:\Users\...'`, exposing .NET text and the local path. `--sigstore-bundle x.json` alone printed `curl: (2) --sigstore-bundle needs ...` with no try-help line, because the check ran in the transfer hook.

## Decision

`CacheDirectory` validates its path when it is built (full-path resolution and each name checked for characters the platform refuses) and throws `ArgumentException("--cache-dir cannot use '<path>'")`. `SigstoreExtension.HookFor` refuses a bundle without `--sigstore-identity` and `--sigstore-root` the same way. Both follow ADR-1289: `curl: <message>`, the try-help line, exit 2, no OS text.
