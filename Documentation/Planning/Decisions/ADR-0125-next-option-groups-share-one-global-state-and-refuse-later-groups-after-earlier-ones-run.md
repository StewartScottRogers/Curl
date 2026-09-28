# ADR-0125 — `--next` option groups share one global state, and a later group's setup refusal comes after the earlier groups run

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-508.

## Context

`-:`/`--next` splits a command line into option groups. curl keeps each group's options in
its own `OperationConfig` and a few in one `GlobalConfig`; its manual marks only some of the
global ones. BL-508 measured curl 8.21.0 (Windows, `Record-CurlExchange.ps1 -Connections 2`;
the bytes are in BL-508's Notes) and found:

- `-v` given only after `--next` shows the first group's transfer too; `-w`, `-o`, `-H` and `-d`
  given before it do not reach the second group.
- `--next` with no URL in its group is refused while parsing on the command line
  (`curl: missing URL before --next`, `curl: option --next: is badly used here`, exit 2), but
  a `next` line in a `-K` file with no URL before it is ignored.
- `curl URL --next` runs `URL`, then prints `curl: (2) no URL specified` and exits 2; a
  form-and-data conflict in a later group likewise comes after the earlier groups have run, while
  one in the first group stops everything.
- A group with an `-o` left over prints `Warning: Got more output options than URLs` once for
  itself and once for every later group, and the later groups do not run.

## Decision

1. `CommandLineOptions` is one group. Every group of a command line holds the same
   `CommandLineGlobalState`, which stores the global settings, so a global option read in any
   group is read through every group's properties without copying. The parser reads each
   argument, and each config file line, into the last group (`CurrentGroup`).
2. `CommandLineOptionTable.GlobalOptionLongNames` lists the global rows: the ones curl's manual
   marks global, plus `-s`, `--variable`, `-V`, `-h` and `-M`, which curl stores in
   `GlobalConfig` without the mark, plus `config`, `next` and `disable`, which act on the command
   line as a whole. A test fails when a row is in neither that list nor the test's per-group
   list, so a new option's place is decided when it is added.
3. `CommandLineParseResult.Options` stays the first group, so the console keeps working
   unchanged for a single group; `Groups` lists every group to run. A setup refusal of the first
   group refuses the command line, as before. A setup refusal of a later group is
   `RefusalAfterGroups`, with `Groups` cut before it. The console prints it after the transfers
   and exits with its code. Only the first group runs until BL-509, but upstream test686
   (`htdhdhdtp://localhost --next`, exit 2) already passes.
4. A missing `-u`/`-U` password is prompted for per group. With more than one group, the prompt
   ends ` on URL #<n>:`, as curl's `checkpasswd` in `tool_paramhlp.c` builds it. It could not be
   measured, because curl reads the password from the console, not from standard input.

## Consequences

- BL-509 runs `Groups` in order and stops after a group with an output left over, as measured.
- A global option has one property on `CommandLineOptions`, as before, so no caller changed.
