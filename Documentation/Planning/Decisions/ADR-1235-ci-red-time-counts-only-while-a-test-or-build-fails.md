# ADR-1235: CI red time counts only while a test or build fails

- Status: Accepted
- Date: 2026-11-04
- Tasks: BL-4647, BL-4648, BL-4649, BL-4715, BL-4716, BL-4717, BL-4778, BL-4797, BL-4798, BL-4799, BL-4823, BL-4824
- Decided by Claude under Stewart's delegation; Stewart approved the rule on 2026-11-04.

## Context

ADR-0761 gives each process finding's task a first criterion: the finding's measurement, run by
`Audit/Tools/Measure-FactoryProcess.ps1` over the live logs and CI runs written after the fix
landed. For the CI red-time findings (AF-0541 to AF-0543, AF-0567 to AF-0569, AF-0614, AF-0624 to
AF-0626, AF-0644) the expected result is "no red spell over 30 minutes". ADR-1234 made the fixes.

The tool counts a red spell from the end of any CI run whose conclusion is `failure` to the end of
the next successful run. On 2026-11-03 the windows after ADR-1234's fixes held red spells in which
no test failed and nothing failed to build:

| Run | Commit | Non-success jobs | Cause |
| --- | --- | --- | --- |
| 41688300433 | 28cbd2df | `Audit paths untouched by the dark factory` cancelled; `Audit self-tests (macos-latest)` cancelled | "The job has exceeded the maximum execution time of 5m0s" in `actions/checkout`; "The job was not acquired by Runner of type hosted even after multiple attempts" |
| 41715208450 | 2533dfc6 | `Build and test (macos-latest)` cancelled | "The job was not acquired by Runner of type hosted even after multiple attempts" |
| 41723085402 | 441bcff2 | `Audit paths untouched by the dark factory` cancelled; `Audit self-tests (macos-latest)` cancelled | checkout ran past the 5-minute `timeout-minutes`; macOS runner not acquired |

Read with `gh run view <id> --json jobs` and each job's annotations
(`gh api repos/StewartScottRogers/Curl/check-runs/<job id>/annotations`). They made spells of
122.97 minutes (41688300433, 18:20:55Z to 20:23:53Z) and 71.8 minutes (41715208450, 21:35:59Z to
22:47:47Z), so seven tasks went to `Blocked` with "criterion 1 cannot hold as written". No factory
behaviour can shorten such a spell: GitHub did not give the run a runner, or a job that tests
nothing (a checkout) ran out of time. The audit guard's checkout was given a blobless fetch and 15
minutes on the `audit` branch (BL-4782, 624133845); runner capacity is GitHub's.

## Decision

CI red time with no failing test does not count against the factory's process criteria. A failed
CI run counts as red only when one of its jobs has a failing test or a build error. These failures
are infrastructure, not factory behaviour, and are left out as a cancelled run already is:

- a job cancelled because GitHub never assigned it a runner ("The job was not acquired by Runner");
- a job that ran into its `timeout-minutes` while testing nothing (checkout, setup, other infrastructure
  steps); a test step that hangs into the timeout is a failing test and still counts;
- a run cancelled because a newer commit superseded it (already left out by the tool).

So a red spell runs from the end of the first run with a failing test or build error after a success
to the end of the next successful run; runs whose only failures are infrastructure neither start nor
end a spell.

For each process task the window starts at its fix's landing. A task with no fix yet starts its window
at the later of its fix's landing and this ADR's commit. Its Notes give each red spell's runs with the
classification of their non-success jobs, so a reader can check them.

The factory itself computes no red-time number: its CI watch only reads failures to file tasks, and
looks every minute while the newest run read is red (ADR-1234). So no factory code changes for this
rule. `Audit/Tools/Measure-FactoryProcess.ps1` is an audit path, which only an interactive session on
the `audit` branch may change, so the same rule for the audit office's own measurement is filed as an
interactive-only task.

## Consequences

- The seven blocked red-time tasks are judged on red time a factory change could have shortened. Until
  the audit tool follows the rule, a task's Notes give both the tool's raw numbers and the numbers with
  the rule applied, and say which runs were left out.
- A red spell caused only by GitHub runner capacity is no longer evidence against the factory, so it no
  longer files Re-fix work or blocks a process task.
- A real test or build failure that GitHub also failed to run elsewhere still counts from the run that
  showed it.
