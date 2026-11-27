# ADR-1443: Every numbered tag publishes its packages to nuget.org with Trusted Publishing, as Surl does

- Status: Accepted
- Date: 2026-11-26
- Asked for by Stewart (2026-11-26: "I want to use Trusted Publishing", then "look in the Surl repository and do it the same way", choosing that every tag publishes, preview tags included) (BL-2393)

## Context

BL-2393 publishes Curl's packages to nuget.org: `Surly.Curl.Api`, `Surly.Libcurl`, `Surly.Curl.Mcp` and its six per-platform packages. Until 2026-11-26 they stayed off nuget.org until the port was complete, and the plan was for Stewart to paste a long-lived API key for each push.

Surl publishes through nuget.org's Trusted Publishing (Surl BL-1795): its `release.yml` release job, on a `v*` tag, runs `NuGet/login@v1` as user `StewartScottRogers`, which exchanges the job's OIDC token for an API key that lasts one hour. It then pushes with `--skip-duplicate`. A nuget.org trust policy on Stewart's account names Surl's repository and `release.yml`.

## Decision

Curl does the same in its own `release.yml`. On a `v*` tag, after creating the GitHub release, the release job (`id-token: write`):
- logs in with `NuGet/login@v1` as user `StewartScottRogers`;
- checks that the release has exactly nine `.nupkg` files at the tag's version;
- pushes each to nuget.org with `--skip-duplicate`.

The rolling `preview` (pushes to `master`) never reaches nuget.org. Pre-release tags publish too, including the `v<x.y.z>-preview.<n>` tags an interactive session cuts for the Surly book under Stewart's standing approval. This lifts the earlier hold, "no nuget.org push until the port is complete".

## Consequences

- No nuget.org API key exists in the repository, its secrets, a log or a terminal.
- Stewart sets up two things on nuget.org, once: the `Surly.` prefix reservation, and a Trusted Publishing policy for owner `StewartScottRogers`, repository `Curl`, workflow `release.yml` (no environment).
- Until that policy exists, a tag's release job fails at the login step after the GitHub release is created. The fix is to add the policy and rerun the job; `--skip-duplicate` makes the rerun safe.
- A version on nuget.org cannot be deleted, only unlisted, so every tag is a lasting public release.
