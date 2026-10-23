# ADR-0747 — Upstream case 1478 stays off the ratchet: it checks upstream's C source against upstream's docs

- Status: Superseded by BL-3402 (the harness's `managen` interpretation carries out `listhelp`, so case 1478 is on the ratchet). Decided by Claude under Stewart's delegation: the interpretation can reproduce `listhelp` from the vendored docs, which makes 1478 a faithful check that upstream's docs and committed C file agree.
- Date: 2026-10-21
- Task: BL-2850
- Decided by Claude under Stewart's delegation.

## Context

BL-2850 runs upstream curl 8.22.0's option-documentation checks against Curl. Cases 971, 1486,
1707, 1708 and 1710 now pass through C# interpretations of their Perl scripts
(`UpstreamTest971Script`, `UpstreamTest1486Script`, `UpstreamTest1707Script`) and are on
`PassingUpstreamCases.txt`. Case 1478, "src/tool_listhelp.c is in sync with docs/cmdline-opts",
is different:

- Its command is `scripts/managen ... listhelp docs/cmdline-opts/*.md`: upstream's 1,000-line
  manual generator in its `listhelp` mode, which prints a C source file - the `helptext[]`
  array of `struct helptxt` initialisers, with its licence header and `#include` lines.
- Its expected standard output is `%include %SRCDIR/../src/tool_listhelp.c%`, upstream's own
  C file.

Both sides of the comparison are upstream files. Neither reads anything Curl produces: the
case checks that upstream's committed C source was regenerated after its Markdown docs changed.
Curl has no `tool_listhelp.c`; its counterpart is `CurlHelpTable` in `Curl.Cli.UnitLibrary`.

## Decision

- (Superseded: 1478 is now on `PassingUpstreamCases.txt`.) Case 1478 was not ported and stayed off it. The harness registers a
  `managen` script (`UpstreamPerlCommandScripts.Managen.cs`), so it no longer skips 1478 as an
  unrun perl command; that script carries out only `mainpage` and `ascii`, not `listhelp`, so
  1478 does not pass and is left off the ratchet.
- What 1478 protects in upstream - that `curl --help` lists each option with its `Help:` line
  from the docs - is already pinned for Curl, more strictly, by `CurlHelpTextTests` in
  `Curl.Cli.UnitTests`: it compares Curl's `--help`, `--help all` and every `--help <category>`
  byte for byte with what the reference curl 8.22.0 build printed (`HelpReference/*.txt`), and
  that output is made from the `tool_listhelp.c` case 1478 checks. Cases 1707, 1708 and 1710
  also check `curl -h <option>` against the docs.
- Interpreting `managen listhelp` to print C source from Curl's help table was rejected: it
  would test a C file Curl does not have, and its only real check (help lines equal the docs')
  would repeat `CurlHelpTextTests`.

## Consequences

- (Superseded by BL-3402.) The gap office's behaviour area kept 1478 `excluded`, for this reason
  (upstream-source-check: it compares two upstream files).
- If upstream's `--help` text changes in a new targeted version, `CurlHelpTextTests` fails
  once the reference output is re-recorded, which is the signal 1478 gives upstream.
