# ADR-0422 — Audit findings close on mechanical evidence

- Status: Accepted
- Date: 2026-10-07
- Task: none (interactive audit-office work on the `audit` branch; Stewart said "go" on 2026-10-07)
- Decided by Claude under Stewart's delegation.

## Context

ADR-0267's audit office closed a finding only when a re-audit by its own auditor, reliable on
that audit's scorecard, said the reproduction no longer reproduces. An auditor is reliable only
when it catches every defect planted for it. A review on 2026-10-07 found that accepted findings
never closed:

- The quality auditor has never been reliable (0/2, 0/1, 0/2, 1/3, 1/2 planted defects caught),
  so its 15 "reproduces: no" re-audits were discarded; AF-0005 and AF-0010 said "no" on four
  audits running. Nothing escalated, timed out or offered another way to close.
- `Invoke-MutationTest.ps1` could not target one site. A re-audit sampled 40 mutants with seed 0,
  so a finding's site was often not sampled, and the auditor recorded "reproduces: no" for a
  site it never ran. One red baseline test (AF-0026 in Networking, AF-0009 in Cli) stopped the
  tool outright.
- `New-TasksFromAcceptedFindings.ps1` skipped any finding with a task, so one that reproduced
  again after its task was Done (AF-0009, AF-0026, AF-0031, AF-0037) never got new work.
- Findings were deduplicated on the auditor's free-text key: AF-0030 and AF-0036, and AF-0031
  and AF-0037, are the same mutants in other words.
- `Test-AuditDue.ps1` knew only no-audit, factory-script and milestone; an audit run with
  `-Auditors` silently skipped the other auditors' open findings; and findings held by an
  interactive-only task were never surfaced.
- The 2026-10-07 audit's quality re-audits ran in the planted tree. AF-0009's "reproduces: yes"
  (281 options expected, 282 counted) was planted defect PD-303 (the `--path-as-is` row removed),
  and AF-0026's (IndexOutOfRange at `TlsReader.cs:98`) was PD-103 (the bounds check in
  `TlsReader.Take` removed). On the clean commit both tests pass.

## Decision

Reliability keeps its job - it scores each auditor's catch rate and says how far its new
findings and its unsupported word can be trusted - but closing an accepted finding needs
evidence, not a perfect plant score.

1. **Targeted reproduction.** `Invoke-MutationTest.ps1 -Site <file>:<line>[:<operator>]` mutates
   exactly one site (found again inside its member when its line moved) and reports `killed`,
   `survived` or `site-missing`. `-ExcludeBaselineFailures` leaves out tests that already fail
   unmutated instead of stopping. Each mutant names its member. A quality finding carries
   `reproduction: mutation <file>:<line>:<operator>` and the targeted `-Site` command; a site the
   auditor could not run is `"reproduces": null`, recorded "not re-audited", never "no".
2. **Closure.** An open finding closes when its own auditor's re-audit says it no longer
   reproduces and, first match wins (`closed-how`):
   - `mechanical`: the audit run's own rerun of its mechanical reproduction agrees (the targeted
     mutant is killed), whatever the auditor's reliability;
   - `reliable-reaudit`: no mechanical answer, and the auditor is reliable;
   - `consecutive`: no mechanical answer, and its previous re-audit line was also its own
     auditor's "no", on a different audit; `closed-by` names both scorecards.
   A mechanical rerun in which the mutant survives closes nothing.
3. **Clean commit, not the planted tree.** The runner's mechanical reruns check out the audited
   commit itself (`Invoke-MutationTest.ps1 -Commit`), never the planted worktree. An auditor's
   verdict, or a "still reported" repeat, on a finding whose location depends (by project
   references, transitively) on the project of a defect planted for that audit, or whose
   evidence names the planted file, is set aside: recorded "not re-audited | overlaps planted
   defect PD-###" and counted as neither yes nor no. AF-0009's and AF-0026's 2026-10-07 lines are
   kept and marked so; no Re-fix task is filed for them.
4. **Stewart's close and stuck findings.** Stewart alone may close an accepted finding himself
   (`closed-how: stewart`). The scorecard gains an Attention section, first, listing every accepted
   finding still open after more than 3 audits that ran its auditor, and every auditor an audit
   left out that has accepted findings.
5. **Reopen loop.** A finding keeps a `tasks` history (its Fix task, Re-fix tasks, a duplicate's
   tasks); `task` keeps meaning the current task. When every task is Done and its latest re-audit -
   its own auditor's "yes", dated after the last completion - says it still reproduces,
   `New-TasksFromAcceptedFindings.ps1` files `Re-fix AF-####: <title>`. A finding with any open
   task, including a Re-fix task a session filed by hand (found by its title), gets nothing new.
6. **Mechanical dedupe.** A surviving mutant's key is `quality:<file>:<member>-<operator
   word>:surviving-mutant`, built from its site, so differently worded reports of one mutant match.
   Open findings sharing a key close as `duplicate` (with `duplicate-of`) of the lowest ID, whose
   `tasks` gains theirs. AF-0036 (of AF-0030) and AF-0037 (of AF-0031) are closed so.
7. **Visibility.** `Test-AuditDue.ps1` reports `reaudit-pending:<n>` (accepted findings whose tasks
   are all Done with no re-audit since) and `interactive-stuck:<n>` (accepted findings held by an
   open task that is `lane: no` or touches an audit path). `RunAudit.ps1` warns, in its output and
   log, when `-Auditors` leaves out an auditor with accepted findings.

The finding template gains `reproduction`, `tasks`, `duplicate-of` and `closed-how`. Open findings
were migrated; closed and rejected ones keep their old front matter, never edited, and a missing
key reads as empty.

## Consequences

Accepted quality findings can close on the next audit whose quality re-audit says "no", once the
runner's own targeted rerun kills the mutant - the 15 discarded re-audits are no longer wasted
work. The rerun costs one build and test of the library's twin per finding it is needed for. A
finding without a mechanical reproduction still needs a reliable auditor or two "no"s in a row,
so a lying auditor must lie twice; the consecutive path is the weaker one, and its `closed-how`
and both scorecards in `closed-by` let Stewart audit it. When code moves to another member, a
targeted site is `site-missing` and reads as no mechanical answer until a session moves the
reproduction's site (done for AF-0030, now in `TurnOffNagle`). The planted-defect set-aside is
deliberately broad: a plant in a widely referenced library sets aside re-audits across its
dependants for that audit.
