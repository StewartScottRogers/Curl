# ADR-1301: curl-mcp lets -o discard to the null device (BL-5516)

Decided by Claude under Stewart's delegation.

## Status

Accepted, 2026-11-18.

## Context

`curl-mcp` confines every path to `CURL_MCP_ROOT`. `-s -o /dev/null -w "%{http_code}" URL`, curl's usual way to
keep only a status code, was refused: `/dev/null` resolved outside the root and `NUL` could not be resolved at all.

## Decision

The `output` option (`-o`) alone accepts two spellings as discard-only targets, checked before path resolution:
exactly `/dev/null` (as curl's documentation spells it, on every platform) and `NUL` in any letter case. The null
device is neither inside nor outside the root; it holds no data, so it opens nothing to read or leak.

Only whole-value matches count: `/dev/null/x`, `/dev/null.txt` and `../NUL.txt` go through the confinement as
before. Every other path option (`-D`, `--trace`, `-c`, ...) keeps today's confinement, since they could be
pointed at a device that is not a discard. `nul:` and `\.\NUL` are not accepted: curl itself does not treat
them as the null device. `NUL` is accepted on every platform because off Windows it is an ordinary relative name
that already resolves inside the root, so allowing it adds no access.

## Consequences

An agent can read a status code without downloading the body into the root. The rule lives in
`McpOptionValuePaths.WrittenFilePaths`.
