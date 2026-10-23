# ADR-0803 — Upstream's source-analysis perl cases are interpreted over the vendored upstream files

- Status: Accepted
- Date: 2026-10-23
- Task: BL-3406
- Decided by Claude under Stewart's delegation.

## Context

ADR-0718 (BL-2852) and ADR-0755 (BL-2851) recorded upstream cases 1135, 1140, 1165, 1167, 1169,
1173, 1185, 1275, 1279, 1544, 745 and 1276 as `no counterpart` because "Curl has no C headers".
ADR-0749 (BL-2991) then ran 1119, 1175 and 1477 through C# interpretations (ADR-0714) that read
the vendored upstream headers, on the reasoning that these cases check upstream's tree for
consistency and the vendored files under `Curl.Conformance.UnitTests/UpstreamTestData` are that
tree. That reasoning holds for every case below. Stewart (2026-10-23) wants every test active and
passing on Windows, and the root CLAUDE.md allows no row whose reason is difficulty or a missing
BCL primitive. Each case's `<command>` line and script were read for this decision.

## Decision

Every case below is `interpret`: a registered C# interpretation per script name
(`UpstreamPerlCommandScripts.Test<N>.cs`, ADR-0714), with its own port task.

| Case | Row | What it needs | Port task |
|------|-----|---------------|-----------|
| 1140 | interpret | `docs/libcurl/*.md` and `docs/cmdline-opts/*.md` (vendored) turned into `.3` and `.1` pages by Curl's cd2nroff (`UpstreamCd2nroffTests`, ADR-0750), the way 1222 does; the script then checks their nroff syntax | BL-3408 |
| 1165 | interpret | `configure.ac` and the `CURL_DISABLE_` macros in upstream's `lib/` and `src/` C sources, to be vendored; the script is text matching | BL-3409 (files: BL-3407) |
| 1169 | interpret | The case checks that the runner accepts `-L <library>` and exits with the library's code. The C# runner can honour `-L`: it reads the library file and honours its `exit <n>` statement (the only statement the case's library holds) | BL-3410 |
| 1173 | interpret | The vendored `docs/libcurl/symbols-in-versions` and the generated `.1` and `.3` pages, as for 1140 | BL-3411 |
| 1182 | interpret | The C# runner can honour `-E <file>` (lines `test:<n>:<reason>` skip test n and print the reason on stdout, exit 1). The precheck line `mkdir`, `cp` and `echo ... > file` is interpreted by the harness as those three commands only, with `;` between them | BL-3412 |
| 1185 | interpret | `scripts/checksrc.pl` run on a C file the case itself writes (`-bmagicbad -balsobad`); the interpretation reimplements the checks that file exercises, and needs no extra vendored file | BL-3413 |
| 1275 | interpret | Capital letters after a period in the Markdown docs under `docs/`, to be vendored; the script is text matching | BL-3414 (files: BL-3407) |

## Supersedes

Superseded by ADR-0749's reasoning, each now `interpret`:

- ADR-0718's rows for 1135 and 1279 (both `test1135.pl`, the latter with `--heading=EXPORTS`): BL-3393; and 1167: BL-3394.
- ADR-0755's rows for 745: BL-3392; 1276: BL-3395; and 1544: BL-3396.

ADR-0718's rows for 1140, 1165, 1169, 1173, 1185 and 1275 are superseded by the table above.
ADR-0718's other rows (1705, 1706 and the manual generator) stand.

## Consequences

Until each port task lands, the cases keep their skip reason and stay off the ratchet.
BL-3407 vendors the extra upstream files 1165 and 1275 read.
