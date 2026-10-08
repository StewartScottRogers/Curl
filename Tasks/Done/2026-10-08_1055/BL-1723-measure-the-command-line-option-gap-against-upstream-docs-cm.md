---
id: BL-1723
title: Measure the command-line option gap against upstream docs/cmdline-opts with Gap/Tools/Measure-OptionGap.ps1
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1721, BL-1722]
touches: [Gap/Tools/Measure-OptionGap.ps1, Gap/Tools/Fixtures/options, Gap/Upstream/8.21.0/options.json]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1723 — Measure the command-line option gap against upstream docs/cmdline-opts with Gap/Tools/Measure-OptionGap.ps1

## Goal

`Gap/Tools/Measure-OptionGap.ps1` does two things. It builds the `options` upstream
inventory from a release's `docs/cmdline-opts/*.md`. It then measures every option's facets
on `Curl.Console` against the matched reference curl, and writes the `options` area
measurement.

## Context

This is ADR-0433 decision 2, area `options`. The inventory, measurement and key formats are
in `Gap/Instructions/Gap-Format.md` (BL-1720). Use `Gap/Tools/Get-UpstreamRelease.ps1`
(BL-1721) for the release folder and `Gap/Tools/Invoke-GapProbe.ps1` (BL-1722) for every
probe.

**Upstream inventory.** One item per `docs/cmdline-opts/*.md` file, excluding `_*.md` (the
man page's sections) and `MANPAGE.md`, which documents the front-matter format. Read
`MANPAGE.md` in the release first. Its description of the front-matter fields (`Long`,
`Short`, `Arg`, `Protocols`, `Added`, `Multi` and the rest) decides how to parse them, and
which options have a `--no-` form. Do not guess from memory. curl 8.21.0's folder holds 299
entries, `_*.md`, `MANPAGE.md` and build files included. Record `introducedIn` from `Added:`.

**Facets.** Each option gives up to four item keys:

- `options:--<long>`: the option is recognised;
- `options:--<long>:alias`: the short form gives the same answer (only when `Short:` exists);
- `options:--<long>:argument`: the option given last, with nothing after it, gives the same
  answer as the reference (this is what tells whether it requires an argument; only for
  options with `Arg:`);
- `options:--<long>:no-form`: `--no-<long>` gives the same answer (only for options that
  `MANPAGE.md`'s rules say have one).

**Probing.** Recognition: run `--<long>` (with a placeholder argument when the option takes
one, chosen from the `Arg:` text: a number for `<seconds>`, `<num>` and the like, an
existing temporary file for `<file>`, `x` otherwise) and no URL, through both binaries with
`Invoke-GapProbe`. A facet is `match` when the exit code and the stderr text are equal, and
`gap` otherwise, with both answers in `expected` and `actual`. When the probe reports no
matching reference, the facet falls back to the documents. It is `match` when Curl's stderr
does not say the option is unknown, and `gap` when it does. Measure the reference's
unknown-option text on the reference curl first and pin it in a fixture. Never write it
from memory. The measurement then records `referenceFallback: "docs"`.

**Parameters.** `-UpstreamRoot` (default: the output of `Get-UpstreamRelease.ps1`),
`-Version` (default the target), `-Candidate` (default `Get-GapCandidateCurl`),
`-OutFile` (the measurement), `-InventoryOnly` (write only the inventory to
`Gap/Upstream/<version>/options.json` and probe nothing; the release watcher BL-1735 runs
this on Linux, so this path uses no Windows-only API), `-SelfTest`.

**Self-test.** Use a fixture folder `Gap/Tools/Fixtures/options/` holding a few fake option
files: one with a `Short:`, one with an `Arg:`, one boolean, one `_SECTION.md` and one
`MANPAGE.md` to skip. Feed it canned probe results through a `-ProbeResults` parameter
(a JSON file of per-command answers), so no binary is needed.

Commit `Gap/Upstream/8.21.0/options.json`, generated from the real 8.21.0 release.

## Acceptance criteria

- [x] `Gap/Tools/Measure-OptionGap.ps1 -SelfTest` prints `PASS` lines and no `FAIL` under Windows PowerShell 5.1 and PowerShell 7. It checks: `_*.md` and `MANPAGE.md` are skipped; each facet appears only when its condition holds; equal answers give `match`; a different exit code gives `gap` with both answers recorded; the docs fallback marks an unknown-option answer as `gap`; `counts.x` and `counts.y` follow the formula in `Gap-Format.md`.
- [x] `Gap/Upstream/8.21.0/options.json` is committed. It holds one item per upstream option file at 8.21.0, each with `introducedIn` from `Added:`, and is valid against `Gap-Format.md`'s inventory format.
- [x] A real run on Windows against a Release `Curl.Console` writes a measurement with every option's facets and states the reference's version line. Its counts are recorded in this task's Notes.
- [x] `-InventoryOnly` runs under `pwsh` with no `Curl.Console` and no reference curl present.
- [x] The header help documents every parameter and the facet rules. The script is ASCII only.

## Notes

- No-form rule: MANPAGE.md's field list says only that `Multi: boolean` marks a boolean option; it
  does not name a `--no-` form. curl's tool_getparam accepts `--no-<name>` for boolean options, so
  `noForm` is true when `Multi` is `boolean` and the long name does not itself start with `no-`
  (for `--no-buffer` the negation would be `--buffer`, which is not a `--no-` form). 101 options are
  boolean at 8.21.0.
- Placeholders: `1` for numeric args, a temporary file for `<file>`/`<filename>`/`<path>`, a
  temporary folder for `<dir>`, `x:x` for `<user:password>`, `x` otherwise. A bare `x` for
  `--user` made the reference wait at a password prompt until the probe's timeout and gave 4
  false gaps on the first run; `x:x` removes the prompt.
- Probe results JSON for `-ProbeResults`: `{ "reference": <line or null>, "answers": { "<args joined
  by spaces>": { "reference": { "exitCode", "stderr" }, "candidate": { ... } } } }`.
- Unknown-option text measured on the reference (Git for Windows mingw curl 8.21.0, Schannel):
  `curl: option --zzz-nonexistent: is unknown` plus the `try 'curl --help'` line, exit 2; pinned in
  `Gap/Tools/Fixtures/options/reference-unknown-option.txt`.
- Inventory: 274 items (299 entries minus 19 `_*.md`, `MANPAGE.md` and 5 build files); every
  item has `Added:`.
- Real run 2026-10-08 on Windows, Release `Curl.Console`, reference
  `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP`:
  573 facets, match 567, gap 6, unmeasured 0, excluded 0, x 567, y 573 (about 3.5 minutes).
  Gaps: `--dns-interface`, `--dns-ipv4-addr`, `--dns-ipv6-addr`, `--dns-servers`, `--ech` (the
  reference build lacks c-ares/ECH and says "the installed libcurl version does not support
  this"; Curl accepts them) and `--proxy-http3:no-form` (the reverse). These are candidates for a
  `reference-lacks:<feature>` exclusion once the measurement reads the reference's `curl -V`.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Measure-OptionGap.ps1 builds the options inventory and measures 573 facets; self-test passes on 5.1 and 7
