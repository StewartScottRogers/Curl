# ADR-1425: --cache-dir refuses a path under an existing file (BL-6032)

Decided by Claude under Stewart's delegation.

## Status

Accepted

## Context

Fitness finding EXT-111: `--cache-dir recfile/sub`, where `recfile` is a file, exited 0 with no message and cached nothing. ADR-1304's activation check refused only a path that is itself a file.

## Decision

`CacheDirectory` refuses a path when it, or any directory above it, is an existing file, with ADR-1304's refusal (`curl: --cache-dir cannot use '<path>'`, exit 2). A refusal, not a `Warning:` line: it matches the answer for a path that is itself a file, and the directory can never be created. A path whose parents are merely missing is still accepted and created on the first write.
