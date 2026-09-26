# ADR-0029 — The output options pair with URLs as curl's tool pairs them

- **Status:** Accepted
- **Date:** 2026-09-26

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

## Context

BL-194 parses `-w`/`--write-out`, `-O`/`--remote-name`, `--remote-name-all`,
`-J`/`--remote-header-name`, `--output-dir` and `--create-dirs` into `CommandLineOptions`.
Until now `-o` values were a plain list, `OutputFiles`, which `Curl.Console` pairs with
`Urls` by position. `-O` takes a pairing position just as `-o` does, `--remote-name-all`
changes the default for URLs that have no output option, and `--no-remote-name` sometimes
takes a position and sometimes does not.

Measured with the reference build (ADR-0018: curl 8.21.0, mingw) on 2026-09-26 against
`file:///C:/Windows/win.ini` (`u`, `u1`, `u2` below), in an empty directory:

| Arguments | Where the bodies went | After-transfer warning |
| --- | --- | --- |
| `-o x -O u1 u2` | `x`, `win.ini` | none |
| `-O u1 u2` | `win.ini`, standard output | none |
| `-O -O u` | `win.ini` | `Got more output options than URLs` |
| `u --remote-name-all` | standard output | none |
| `u1 --remote-name-all u2` | standard output, `win.ini` | none |
| `--remote-name-all -o x u` and `--remote-name-all u -o x` | `x` | none |
| `--remote-name-all --no-remote-name u` | standard output | none |
| `--remote-name-all --no-remote-name --no-remote-name u` | standard output | `Got more output options than URLs` |
| `--no-remote-name --no-remote-name u` | standard output | none |
| `-o x --no-remote-name u` | `x` | none |

This is curl's tool keeping one list of nodes: a URL fills the first node without a URL,
an output option the first node without an output, and a node created while
`--remote-name-all` is on starts out using the remote name. `--no-remote-name` creates no
node when every node already has an output and `--remote-name-all` is off.

## Decision

- **`CommandLineOptions.UrlOutputs` mirrors curl's node list.** One `UrlOutput` per node,
  with `Url`, `FileName` (`-o`) and `UsesRemoteName` (`-O`, or `--remote-name-all` when the
  node was created); `FileName` wins over `UsesRemoteName`. The after-transfer warning is
  raised when a node has an output option and no URL, replacing the old count comparison.
- **`OutputFiles` becomes positional.** It is the `-o` name of each `UrlOutputs` entry up
  to the last one that has one, `null` for an entry before it with none, so
  `Curl.Console`, which reads `OutputFiles[index]` for the Nth URL, keeps writing each `-o` file with the right URL even when `-O` or a kept
  `--no-remote-name` sits between them (`-O -o x u1 u2` gives `x` to `u2`). Found by
  review: a plain list of `-o` names would have handed `x` to `u1`.
- **`-w @file` keeps curl's reading.** The template is the file's bytes with every CR, LF
  and NUL removed, decoded as UTF-8, as `-d @file` is read; a file with no bytes clears the
  template and warns `Warning: Failed to read <file>` (`<stdin>` for `@-`) unless `-s` came
  first. An unreadable file is the `-d @file` refusal, exit 26.

## Consequences

- The console layer reads `UrlOutputs` for each URL once `-O`, `--output-dir` and
  `--create-dirs` are wired; until then `-O` parses and a remote-named URL goes to
  standard output.
- A `-w` template holding bytes that are not UTF-8 is decoded lossily; curl writes them
  back verbatim. Write-out expansion is a later task and can revisit this if it matters.

## Alternatives considered

- **A second list of `-O` flags beside `OutputFiles`.** Cannot express where `-O` sits
  relative to `-o`, nor `--remote-name-all`'s creation-time default.
- **Resolve every URL's output at the end of parsing.** Loses the ordering rules above,
  which depend on what was on the list when each option was read.
