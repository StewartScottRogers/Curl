# ADR-0564 — The MCP server checks a whole curl argument array against the root before curl runs

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2379.

## Context

ADR-0548 decision 4 refuses the `curl` tool before any transfer starts when a path it would use
lies outside `CURL_MCP_ROOT`. ADR-0557 resolves one path and ADR-0559 says how each option uses
one. What is left is reading a whole argument array the way curl does, finding the paths inside
values, and deciding the cases where a path cannot be known before curl runs.

## Decision

1. `McpArgumentChecker.Check` answers with an `McpArgumentCheck`: allowed, or refused for the
   first offending option as the user wrote it (`-o`, `--output`, `URL` for a URL argument), the
   path, and a reason. Later violations are not reported.
2. `Curl.Mcp.UnitLibrary` now references `Curl.Cli.UnitLibrary` and reads
   `CommandLineOptionTable.Rows` (long name, letter, takes a value, takes a subject, ends a
   bundle), replacing ADR-0559 decision 2's "the library does not". `Curl.Cli`'s parser itself
   is not reused: it applies every value, reading `-d @file` and `-K` files and prompting for
   passwords. The checker tokenises as that parser does: bundles, attached values,
   `--name=value`, `--no-` and `--expand-`, `--next`, the first `--`. A row the path table does
   not classify is unknown and refused.
3. Embedded paths are found as curl reads them: `@file` for `-d`, `--data-ascii`,
   `--data-binary`, `--json`, `-H` and `--proxy-header`; whichever of `=` and `@` comes first for
   `--data-urlencode`, `--url-query` (after `+`) and `--variable` (after `%`); `name=@file`,
   `name=<file` and `;headers=@file` for `-F`, the name ending at `;` or quoted, each
   comma-separated name checked; `-w @file` and every `%output{file}` / `%output{>>file}` in a
   template (`%%` is literal); a `localhost` proxy URL's path as a Unix socket; `-b` without `=`;
   `--pinnedpubkey` unless `sha256//`; `-z` after its `-`, `+` or `=`. `-`, `@-` and `-T .` are
   standard streams and allowed.
4. A `file:` URL, positional or `--url`, is checked as its path: no host, `localhost` and
   `127.0.0.1` are local; any other host becomes a `//host/path` share path, which lies outside;
   query and fragment are dropped and percent-encoding decoded first, as curl opens the file.
5. Refused because the path cannot be known before curl runs: a `file:` URL or `-T` name with
   glob characters, an `-o` name with a `#<n>` glob reference, an `--expand-` value of a
   path-using option holding `{{`, `--proto-default file` (scheme-less URLs would be read as
   files), `-K -`, `--abstract-unix-socket` (no file system confines it), and `-n` /
   `--netrc-optional` (they read the home folder's `.netrc`). `-O` and `-J` are allowed: they
   write into the working folder, which the `curl` tool sets to the root (BL-2381).
6. `-K` is refused unless its file lies inside the root and can be read; its lines are split as
   `Curl.Cli`'s `ConfigFileSyntax` splits them, checked as options, and a refusal names the file
   and line. Files nest at most 8 deep (`MaximumConfigDepth`), so a config that includes itself
   is refused rather than looping. No other file is read and nothing is written.
7. Not inspected: the contents of a `-w @file` template inside the root, which may hold
   `%output{}`; BL-2460 decides how to close that.

## Consequences

The `curl` tool (BL-2381) calls `Check` first and maps a refusal to
`McpToolFailure.PathRefused(check.Option, check.Reason)`. Some arguments real curl would run
are refused (globbed file names, `#1` outputs); a client rewrites them without the glob.
The check assumes what BL-2381 must provide: curl runs with the root as its working folder, and
reads no default config file (`.curlrc`), which the checker never sees.
