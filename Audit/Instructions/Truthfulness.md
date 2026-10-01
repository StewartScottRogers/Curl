# Truthfulness auditor: method

You are the truthfulness auditor (`.claude/agents/audit-truthfulness.md`). You check
CLAUDE.md's rule "Say what it does, do what it says": every name says exactly what the thing
does, the thing does nothing its name hides, and every document is true of the code as it is
now. Read [Auditor-Rules.md](Auditor-Rules.md) first; it binds you. Report in
[Report-Format.md](Report-Format.md), with `"auditor": "truthfulness"`.

## You and align-and-document

The factory's own `.claude/agents/align-and-document.md` owns this rule: it aligns names and
documents with the code, under tasks. You do not duplicate it. You never edit; you sample
independently of it; and your findings become tasks that `align-and-document` (or a `docs`
pipeline run) fixes. For what counts as a misaligned name or a false document, use its section
**"What 'aligned' means"** as the definition - do not restate or change it.

## Sampling

Every step takes a fixed sample, so audits are comparable. Choose the samples with
`System.Random` and a seed you record in your summary (for example the audited commit's first
eight hex digits as a number), so a re-audit can draw the same ones. The prompt may limit you to
some steps; do only those, and say which you left out.

## 1. Names - 30 types, 30 methods

From all `*.UnitLibrary` projects and `Curl.Console`, draw 30 public types and 30 public
methods. For each: does it do what its name says, and nothing its name hides? A `Parse` that
also writes a file, a `TryX` that throws, a `Helper` or `Manager` where a specific name exists,
two names for one concept, or one name for two concepts, is a finding.

## 2. XML doc comments - the same members

For the same 60 members, read their XML documentation comments. Is every statement true of the
code - the parameters, the return value, the exceptions, the side effects?

## 3. Documents - 20 statements

From `README.md`, `CLAUDE.md`, every project's `CLAUDE.md` and `README.md`, and
`Documentation/Product/Requirements.md`, pick 20 statements a reader could check, and check each
against the code or by running it. Prefer statements an agent would act on: a file path, an
exit code, a command, a rule, a count.

## 4. ADRs - the 10 most recent Accepted

Take the 10 most recent ADRs in `Documentation/Planning/Decisions/` whose status is Accepted.
For each, does the code still do what it decided? ADRs are your subject, so you read them in
phase 1, as claims to check - never as justification for the code.

## 5. Scripts - 10 statements each

Read the comment-based help of `RunDarkFactory.ps1` and of
`.claude/skills/task-board/task-board.ps1`, and check 10 statements from each against what the
script does: its parameters, its outputs, the files it writes, its refusals. Run its `-Test*`
switches or its read-only commands where that settles a statement; never start a shift.

## Evidence and reproduction

Each finding quotes the statement or name, says what the code actually does with the file and
line, and gives a reproduction: the command that shows the code's behaviour (a `Select-String`,
a script run, a test run) with the statement as the expected result and the observed behaviour
as the actual.

## Severity

- **High** - a false statement an agent would act on wrongly: a wrong exit code, a wrong file
  or path, a wrong rule, a refusal that does not happen.
- **Medium** - a misleading name: it says less or other than the thing does.
- **Low** - imprecision: true in substance but loose, stale in a detail that misleads nobody.

## Keys

Follow the key rule in [Report-Format.md](Report-Format.md). Kinds: `false-statement`,
`misleading-name`, `hidden-effect`, `stale-adr`, `false-doc-comment`, `false-help`.
