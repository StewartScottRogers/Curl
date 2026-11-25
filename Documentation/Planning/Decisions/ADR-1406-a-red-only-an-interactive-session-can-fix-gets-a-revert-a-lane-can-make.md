# ADR-1406: A red CI that only an interactive session can fix gets a revert a lane can make

- Status: Accepted
- Date: 2026-11-25
- Asked for by Stewart (2026-11-25: "build the revert option"), from the CI-red analysis of BL-5175..BL-5177

## Context

The audit office holds the factory to at most 30 minutes of red CI on `work/dark-factory`. On 2026-11-21 CI stayed red for 227 minutes:
- BL-5707 (c84035f7) renamed the NuGet package IDs.
- `ci.yml` still checked the old IDs.
- The CI watch filed BL-5710..BL-5712 to fix `ci.yml`. That file is a guard file, so those tasks were interactive only. No lane could take them, and CI stayed red until a person acted.
- Reverting c84035f7 would have turned CI green within one run.

## Decision

When the CI watch files an interactive-only task for a regression, it also files a revert task that a lane can take (`New-CiRevertTasks` in `DarkFactory/CiWatch.ps1`). The revert plan (`Get-CiRevertPlan`) covers every non-merge commit that changes a file outside `Tasks/`, between the last green run and the first failing run.

There is no plan, and no revert task, in any of these cases:
- the failure is flaky, an audit-guard failure or a hang;
- no green run is known;
- the range holds a merge;
- a commit in the range touches an audit path or guard file;
- the range holds more than 3 code commits.

The revert task:
- is titled "Fix CI failure by reverting <sha> while <task> waits for an interactive session", so lanes take it first as a CI fix (ADR-1158);
- names no failing test, so the interactive-only task stays the one that covers the failure, and the "CI needs you" caption stays;
- asks the lane to revert the commits and to file a task that re-lands the work after the interactive-only task.

## Consequences

- CI goes green within about one run of such a break, rather than waiting for a person.
- The reverted work comes back through the re-land task once its interactive-only fix is done.
- A revert undoes other lanes' finished work. The cap of 3 commits, and the refusal of any range that holds a merge or touches a guard path, keep a revert small and inside what lanes may change.
