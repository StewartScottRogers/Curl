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

## Amended 2026-10-07: the clean rerun comes before the set-aside

Decided by Claude under Stewart's delegation.

Audit 20261007-133608 (scorecard `2026-10-07_1336.md`) had a reliable quality auditor (2 of 2
planted defects caught) re-audit 18 findings, and closed one. Eighteen Re-audits lines read
"not re-audited | overlaps planted defect PD-###". AF-0005's reproduction is a mutant in
`Curl.Cryptography.UnitLibrary/BrainpoolPoint.cs`; the auditor ran it and the mutant was killed,
but PD-101 was planted in `AeadChaCha20Poly1305.cs`, another file of the same project, so the
project rule of decision 3 set the verdict aside. `Write-AuditFindings.ps1` set aside before it
reached the runner's own rerun, so the rerun on the clean commit - the evidence decision 2 closes
on, which no plant can touch - never ran. With eight plants spread over the most referenced
libraries, nearly every finding sits in or above a planted project, so fixed findings could not
close at all.

Decisions 2 and 3 now read:

- **The rerun first.** When a finding with a mechanical reproduction is re-audited by its own
  auditor, and the auditor says "no" or a planted defect overlaps it, the runner reruns the
  reproduction on the clean audited commit before anything else. Its answer stands, plant or
  no plant: an own "no" with the mutant killed closes the finding (`mechanical`); a mutant that
  survives is recorded `reproduces: yes (runner rerun on clean commit)` and closes nothing. Only
  a rerun that cannot tell (`site-missing`, a red baseline) leaves the overlap rule to set the
  verdict aside, as does an own "yes" the clean rerun contradicts (a "yes" closes nothing, so it
  waits for the next audit). The rerun's JSON and output stay in the run folder,
  `reports\reruns\<id>.json` and `.log`, with one line per rerun in `reports\reruns\outcomes.txt`.
- **A narrower overlap for the rest.** A finding with no mechanical reproduction is set aside only
  when the planted defect's file is the finding's own file, or the auditor's evidence names the
  planted file - not because its project depends on a planted project. A plant adds a defect, so
  in a dependency it pushes a verdict towards "yes", not "no"; a stray "yes" costs at most a Re-fix
  task that finds nothing to fix, while the broad rule cost every "no". AF-0026's 2026-10-07 line,
  whose evidence named `TlsReader.cs`, would still be set aside; AF-0009's would not. Findings with
  a mechanical reproduction keep the project rule for what the rerun leaves unanswered, because
  any test in the project's closure decides whether a mutant is killed.

The 18 set-aside verdicts of audit 20261007-133608 were evaluated again with the corrected rules.

## Amended 2026-10-14: a past run's cost closes when its task is Done

Decided by Stewart, 2026-10-14 ("change it"); BL-2218.

A process finding about one past run's cost (key `process:logs:<task>:cost-outlier`, for example
AF-0204, "BL-2142 cost $3.11, 4.25 times the median") describes a run that already happened.
History cannot change, so every re-audit said it still reproduced, and once its Fix task was Done
`New-TasksFromAcceptedFindings.ps1` filed a `Re-fix` task (BL-2205 to BL-2208 for AF-0190,
AF-0191, AF-0204 and AF-0205) whose lane could only record that nothing could be fixed. For such
a finding the task is the only evidence there can be: its job is to look for avoidable waste in
what drove the cost and fix the cause, or record that there is none.

Decisions 2 and 5 gain one exception:

- **Closure.** An accepted finding whose key ends in `:cost-outlier` closes with `closed-how:
  task-done-historical`, `closed-by` the audit's scorecard and a reason, at the first audit
  after it has at least one task and every task - its `task`, its `tasks`, and every
  `BL-###-fix-`/`re-fix-` task file naming it, counted as `Test-AuditDue.ps1` counts them - is in
  `Tasks/Done` at the audited commit. `Write-AuditFindings.ps1` closes it before reading any
  report, so a re-audit of it is skipped, and `RunAudit.ps1` neither asks its auditor to re-audit
  it nor has the seeder avoid its file.
- **Reopen loop.** No `Re-fix` task is ever filed for such a finding, whatever its re-audits say.
- **Visibility.** `Test-AuditDue.ps1` counts such findings as `close-pending:<n>` instead of
  `reaudit-pending:<n>`.

Every other finding follows the decisions above unchanged. AF-0190, AF-0191, AF-0204 and AF-0205
were closed under this rule by an interactive session on 2026-10-14 (`closed-by: session`), their
original tasks BL-2149, BL-2150, BL-2197 and BL-2198 being Done on `work/dark-factory`, and their
Re-fix tasks BL-2205 to BL-2208 were moved to Done without a fix.
