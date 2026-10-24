# ADR-0718 — Upstream's C-source and man-page tooling cases have no counterpart, except the manual generator

- Status: Accepted (superseded in part by ADR-0803)
- Date: 2026-10-19
- Task: BL-2852
- Decided by Claude under Stewart's delegation.

## Context

Twelve of upstream curl 8.22.0's cases run a `<command type="perl">` script that checks the
upstream repository itself, not a behaviour of the `curl` command line or of the libcurl API:
1135, 1140, 1165, 1167, 1169, 1173, 1185, 1275, 1279, 1544, 1705 and 1706. The harness skips
them with `the harness does not run a perl command` (BL-2850 adds the mechanism). BL-2852
decides each by ADR-0711's rule: a counterpart exists in Curl (name the task that covers it),
or the case tests a C-only thing (name it). A reason is never "hard" or "the BCL has no
primitive". This run read each case's `<command>` line and the file it names.

## Decision

| Case | Row | The upstream-only thing it tests |
| --- | --- | --- |
| 1135 | no counterpart | the order of `CURL_EXTERN` declarations in libcurl's C headers (`test1135.pl`); Curl has no C headers |
| 1140 | no counterpart | nroff syntax of the `.1` and `.3` man pages under `docs/`; Curl ships no nroff pages |
| 1165 | no counterpart | `configure.ac` against the `CURL_DISABLE_` macros in the C sources; Curl has no autoconf build or `CURL_DISABLE_` macros |
| 1167 | no counterpart | the `curl` prefix of public symbols in the C headers; Curl has no C headers (symbol coverage of `Libcurl` is BL-2851's) |
| 1169 | no counterpart | that Perl's `runtests.pl` accepts `-L <library.pl>`; Curl's runner is C# and has no Perl library option |
| 1173 | no counterpart | nroff syntax of the man pages (the `symbols-in-versions` references it also reads are BL-2851's) |
| 1185 | no counterpart | `scripts/checksrc.pl`, upstream's C style checker; Curl's style is enforced by the C# analyzers and `dotnet format` |
| 1275 | no counterpart | capital letters after a period in upstream's Markdown docs; a style rule for upstream's documents, which Curl does not ship |
| 1279 | no counterpart | `libcurl.def`, a C export list, against the `CURL_EXTERN` declarations (the same script as 1135 with `--heading=EXPORTS`); Curl has no export list |
| 1544 | no counterpart | the OS/400 wrapper (`packages/OS400`) translating every string option; Curl has no OS/400 wrapper |
| 1705 | covered by BL-2881 | `managen` building a man page from `option1.md` and `option2.md`; Curl's manual is stored (ADR-0069), so BL-2881 builds the generator |
| 1706 | covered by BL-2881 | `managen` building an ASCII page from the same documents; BL-2881 |

No row gives difficulty or a missing BCL primitive as its reason: each `no counterpart` row is
a tool for C source, C headers, nroff or Perl that Curl does not have. 1705 and 1706 are different:
a complete reimplementation generates its manual from option documents, so BL-2881 builds that
generator and runs both cases (depending on BL-2850 for the perl-command mechanism).

## Consequences

- The skip reason `the harness does not run a perl command` stays for these cases until BL-2850
  and BL-2881 land; `PassingUpstreamCases.txt` and `UpstreamCaseScreening.cs` are unchanged here.
- No project, option or `--ai-help` text changes.
