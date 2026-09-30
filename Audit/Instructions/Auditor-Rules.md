# Rules every auditor follows

Read this file first, before your own instructions (`Audit/Instructions/<Auditor>.md`).
These seven rules apply to every auditor - quality, security, performance, conformance,
truthfulness and process - and your own instructions add to them, never relax them. They
keep each audit independent of the dark factory it audits and make every finding
checkable by someone else. The design is
[ADR-0267](../../Documentation/Planning/Decisions/ADR-0267-an-independent-audit-office-audits-the-dark-factory-from-outside-its-reach.md).

## The rules

1. **Audit only the tree the prompt names.** That tree is a detached worktree the audit
   run prepared; audit it, never the checkout you were started in. Never change a
   tracked file in that tree. Scratch work goes in the temporary folder the prompt
   names, or in a worktree a tool creates and removes itself.
2. **Two phases.** Phase 1 judges the code, tests and scripts alone: do not open
   `Documentation/Planning/Decisions/`, `Tasks/`, the factory's logs, or commit messages
   until every phase-1 finding is written down - except the sources your own
   instructions name as your subject (the process auditor audits the logs and history;
   the truthfulness auditor audits ADRs against the code), which you read as evidence,
   never as justification. Phase 2 may read them, and may only annotate a phase-1
   finding ("explained by ADR-NNNN: ...") - never delete it; whether an ADR's reason is
   good enough is Stewart's call at triage.
3. **Stay out of the office's own records.** Never open `Audit/PlantedDefects/`,
   `Audit/Findings/` or `Audit/Scorecards/`, and never read another auditor's output.
   The findings you are asked to re-audit are given in the prompt.
4. **No evidence, no finding.** Every finding has evidence (file and line, command
   output, measurement) and a reproduction a stranger can run from the repository root.
5. **Re-audit what you are given.** Re-audit each listed finding by running its
   reproduction, and report whether it still reproduces.
6. **End with one report block.** End with exactly one report block in the format of
   [Report-Format.md](Report-Format.md).
7. **No Python, no network.** PowerShell or C# file-based apps only; no network beyond
   loopback.

## What the rules mean in practice

- A phase-2 annotation goes into the finding's `evidence`, after the phase-1 evidence;
  the finding keeps its severity. Rule 2 lets you explain a finding, not withdraw it.
- A reproduction is the finding's `reproduction.command` in the report: one PowerShell
  command, run from the repository root, with the result a correct tree gives and the
  result the audited tree gave ([Report-Format.md](Report-Format.md#a-finding)).
- A re-audit is judged only by running the reproduction. A task reaching Done, a commit
  message or an ADR saying it was fixed is not evidence that it no longer reproduces.
- Breaking rule 1 or rule 6 makes your audit unreliable: the audit run treats an
  auditor that changed the audited tree, or returned no parseable report block, as
  unreliable for that scorecard ([Scorecards/README.md](../Scorecards/README.md#reliability)),
  and an unreliable re-audit closes nothing.
