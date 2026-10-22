# ADR-0750 — test1139's interpretation stands in for the generated curl.1 with the option files' .IP lines

- Status: Accepted
- Date: 2026-10-21
- Task: BL-2992
- Decided by Claude under Stewart's delegation.

## Context

Upstream case 1139 runs `tests/test1139.pl`, which cross-checks `src/tool_getparam.c`,
`src/tool_listhelp.c` and the `.IP "-o, --option"` lines of `docs/cmdline-opts/curl.1`. `curl.1` is
generated at build time by `scripts/managen` from `docs/cmdline-opts/*.md`; it is in no release tarball,
so the vendored data (BL-2990) has none, and the script would `die "failed getting curl.1"`.

## Decision

- `UpstreamTest1139Script` reads a built `curl.1` when one exists, at the build root first and then the
  release root, as the script does.
- With neither, it stands in for the page with the line `managen` writes for each option: for every
  `docs/cmdline-opts/*.md` file whose front matter (between its first two `---` lines) has a `Long:` line,
  `.IP "-s, --long"` (or `.IP "--long"` without a `Short:` line). Only these lines are read by the check.
- The options not in all three places are reported in ordinal order, where the script's Perl hash order
  is undefined; any order is equally upstream's.
- `test1222.pl` likewise needs generated `.3` pages; with none it prints its own skip line and exits 0,
  which the interpretation does too. Its full check is BL-3000.

## Consequences

Case 1139 runs against upstream's own option files and source lists and passes, as upstream's does. A
`managen` change to how options are listed in `curl.1` would need this stand-in to follow it.
