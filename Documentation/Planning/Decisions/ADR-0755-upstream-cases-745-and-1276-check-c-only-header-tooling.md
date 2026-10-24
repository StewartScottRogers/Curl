# ADR-0755 — Upstream cases 745 and 1276 check C-only header tooling and have no counterpart

- Status: Accepted (superseded in part by ADR-0803)
- Date: 2026-10-21
- Task: BL-2851
- Decided by Claude under Stewart's delegation.

## Context

BL-2851 runs upstream curl 8.22.0's nine libcurl table checks against `Libcurl`'s tables.
Seven of them have C# interpretations over `Libcurl`'s option, symbol, error and feature tables
and are on `PassingUpstreamCases.txt`: 1119, 1175 and 1477 (BL-2991), 1139, 1222 and 1488
(BL-2992) and 1177 (BL-2993). The other two, 745 and 1276, are judged here by ADR-0711's rule
(and ADR-0718's for the tooling cases): a counterpart exists in Curl, or the case tests a C-only
thing, named. A reason is never "hard" or "the BCL has no primitive". This run read each case's
`<command>` line and the script it names.

## Decision

| Case | Row | The upstream-only thing it tests |
| --- | --- | --- |
| 745 | no counterpart | `test745.pl` checks that `include/curl/typecheck-gcc.h`, the GCC-only compile-time type-checking macros for `curl_easy_setopt` and `curl_easy_getinfo`, lists every `CURLOPT_` and `CURLINFO_` of each `CURLOPTTYPE_` that `curl.h` declares; Curl has no C headers and no C compiler macros, and the option types themselves live once, in `CurlOptions.All` |
| 1276 | no counterpart | `test1276.pl` runs `lib/optiontable.pl` over `curl.h` and diffs its output against the generated `lib/easyoptions.c`; Curl generates no C source from a header and has no generator to keep in step: the table that file holds, `curl_easy_option_by_name`'s, is `Libcurl`'s hand-written `CurlOptions.All` |

Neither row gives difficulty or a missing BCL primitive as its reason: each checks the agreement
of two upstream C artefacts (a header and a GCC macro header, a header and a generated C file) that
Curl does not have. As ADR-0749 records for the seven interpreted cases, upstream's headers are
vendored and read as the scripts read them.

## Consequences

- 745 and 1276 keep the skip reason for a perl script without a C# interpretation and stay off
  `PassingUpstreamCases.txt`; `UpstreamCaseScreening.cs` is unchanged.
- No project, option or `--ai-help` text changes.
