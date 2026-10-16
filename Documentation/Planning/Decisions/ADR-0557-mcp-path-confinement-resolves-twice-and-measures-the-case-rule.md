# ADR-0557 — MCP path confinement resolves a path twice and measures the root's case rule

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2377.

## Context

ADR-0548 decision 4 confines every file a Curl MCP tool reads or writes to `CURL_MCP_ROOT`, with
`..` and symbolic links resolved. It leaves open how `..` after a symbolic link is read (Windows
collapses it in the path as written; Unix follows the link first), how names are compared, and
what an unset or bad variable means.

## Decision

1. `Curl.Mcp.UnitLibrary`'s `McpConfinementRoot.Check` resolves a path twice: as written
   (`Path.GetFullPath` against the root, `..` collapsed first) and step by step (each existing
   symbolic link replaced by its resolved target before the next name, so `link/..` leaves the
   link's target). Both are then resolved through every existing link, and the path is inside only
   when both are. One rule serves every platform, and on each it refuses at least what the
   platform's own reading would let escape. A name that does not exist yet is kept as written, so
   a new output file is judged by its nearest existing ancestor; a dangling link is still
   followed, so creating a file through it cannot write outside.
2. Links nesting more than 40 deep are a loop. An invalid path, a loop, an input/output failure
   or a denied access is `Unresolvable`, refused like `Outside`; neither is an exception.
3. The case rule is measured, not assumed from the operating system: when the resolved root's
   path with every letter's case swapped names an existing folder, names compare ignoring case;
   otherwise, or when the path has no letters, they compare exactly, which can only refuse more.
   Inside means equal to the root or starting with the root plus a separator, so `root-evil` is
   not inside `root`.
4. The root itself is resolved through its links (so `/var` and `/private/var` on macOS agree).
   `CURL_MCP_ROOT` unset is `McpConfinementRoot.NotConfigured`, whose every check is
   `NoRootConfigured`; set but empty, relative or not an existing folder throws
   `ArgumentException` naming the cause. The variable is read through an injected
   `Func<string, string?>`.

## Consequences

- A path through a link whose `..` would be harmless on Windows can still be refused there; that
  costs an agent nothing it cannot write directly.
- Windows short (8.3) names are not expanded, so a root given in one form and a path in the other
  is refused rather than allowed.
- Which options carry paths, and checking whole argument arrays, are the later tasks of
  `Documentation/Planning/Curl-Mcp.md`.
