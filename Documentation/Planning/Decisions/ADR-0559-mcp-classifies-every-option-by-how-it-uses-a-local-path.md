# ADR-0559 — The MCP server classifies every curl option by how it uses a local path

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2378.

## Context

ADR-0548 decision 4 confines every file a Curl MCP tool reads or writes to `CURL_MCP_ROOT`.
To do that for the `curl` tool's argument array, the server must know which options carry a
local path and how. Several options do not take a plain path: `-b` takes a file or a literal,
`-O` writes a file named from the URL with no value at all, `-d @file` and `-w @file` hide a
path inside the value, and a proxy may be a Unix socket URL.

## Decision

1. `Curl.Mcp.UnitLibrary`'s `McpOptionPathTable` maps every long name in `Curl.Cli`'s
   `CommandLineOptionTable` to one `McpOptionPathUse`: `None`, `ReadsFile`, `WritesFile`,
   `WritesDirectory`, `ReadsOrWritesFile`, `ReadsFileOrLiteral`, `ValueEmbedded` or
   `UnixSocketPath`, read from the curl 8.22.0 manpage.
2. An option the table does not name is unknown and `TryFind` answers `false`; the server
   refuses it. A drift test in `Curl.Mcp.UnitTests` fails, naming the option, when the option
   table gains a row with no classification, and a second fails when a classified name leaves
   the table. The test project references `Curl.Cli.UnitLibrary` for this; the library does not.
3. Flags that read or write a file they imply rather than name are classified by that file:
   `-O`, `--remote-name-all` and `-J` are `WritesFile`; `-n` and `--netrc-optional` are
   `ReadsFile` (the home directory's `.netrc`). The argument check decides what to do with a
   path-less flag.
4. `--pinnedpubkey`, `--proxy-pinnedpubkey` (a file or `sha256//` hashes) and `-z` (a date or a
   file whose time is used) are `ReadsFileOrLiteral`, like `-b`.
5. `--url` (a `file://` URL), `-x`, `--preproxy` and the `--socks*` proxies (a
   `socks5h://localhost/path.sock` Unix socket URL) are `ValueEmbedded`, with the `@file` forms
   of `-d`, `--data-ascii`, `--data-binary`, `--data-urlencode`, `--json`, `--url-query`, `-F`,
   `-H`, `--proxy-header`, `-w` (also `%output{}`) and `--variable`; BL-2379's whole-argument
   check finds the path inside.
6. `--abstract-unix-socket` is `UnixSocketPath` like `--unix-socket`, although its name lives in
   no file system: classifying it with a path lets the check refuse it rather than pass it.
7. `--data-raw`, `--form-string`, `--create-dirs` and the ignored `--random-file` and
   `--egd-file` are `None`.

## Consequences

- A new curl option cannot reach the MCP server unclassified: its row breaks the drift test
  until someone decides its path use.
- The classification depends on the curl 8.22.0 manpage; moving the targeted version means
  re-reading the new options' entries, which the drift test forces anyway.
