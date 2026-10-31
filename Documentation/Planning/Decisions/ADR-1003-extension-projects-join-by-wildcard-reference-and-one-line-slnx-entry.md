# ADR-1003: Extension projects join by a wildcard reference and one line in Curl.slnx

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4091)

## Context

Over 60 extension tasks each add a `Curl.Extension.<Name>` project pair. Each would edit `Curl.slnx` and the
Composition csproj, so the task board would run them one at a time.

## Decision

- `Curl.Extension.Composition.UnitLibrary.csproj` references `..\Curl.Extension.*.UnitLibrary\*.csproj` by
  wildcard, excluding itself and `Curl.Extension.Abstractions.UnitLibrary`. A new extension edits no line there.
  `VerifyExtensionIsolation` is unchanged: only Composition (a host-side registry) references extensions.
- A new extension adds exactly one line to `Curl.slnx`, under the marker comment in the Extensions section: a
  single-line `<Folder Name="/Extensions/<Name>/">` holding its UnitLibrary and UnitTests `<Project/>` elements.
- `Curl.slnx` is `merge=union` in `.gitattributes` and listed in `Tasks/SharedListFiles.txt`.

## Why one line

Measured with `git merge-file --union`: two branches each adding a multi-line `<Folder>` block at the marker
yield an interleaved, broken result (the identical `</Folder>` lines collapse into one). One self-contained line
per extension merges as two intact lines; a plain merge conflicts. `merge=union` only changes how a *conflict*
is resolved, so a removal in one branch that does not collide with the other's edit still merges normally; the
residual risk (a removal colliding with an addition at the marker) is accepted because extensions are never
removed in bulk.

## Consequences

CLAUDE.md "Extensions" and `DarkFactory/LaneRules.md` state the recipe. Extension tasks need not list
`Curl.slnx` or the Composition csproj in `touches`.
