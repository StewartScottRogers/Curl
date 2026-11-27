# ADR-1443: nuget.org packages are published with Trusted Publishing, by a hand-run, approval-gated workflow

- Status: Accepted
- Date: 2026-11-26
- Asked for by Stewart (2026-11-26: "I want to use Trusted Publishing"); the workflow's shape decided by Claude under Stewart's delegation (BL-2393)

## Context

BL-2393 publishes Curl's packages to nuget.org: `Surly.Curl.Api`, `Surly.Libcurl`, `Surly.Curl.Mcp` and its six per-platform packages. Until now the plan was for Stewart to paste a long-lived nuget.org API key into a terminal for each push. nuget.org's Trusted Publishing removes that key. A GitHub Actions job presents its OIDC token, `NuGet/login` exchanges it for an API key that lives one hour, and the key works only for the workflow that a nuget.org trust policy names. A nuget.org push cannot be undone (a version can only be unlisted), and Stewart holds every push until the port is complete.

## Decision

`.github/workflows/nuget-publish.yml` publishes one numbered release, and nothing else:

- It runs only by hand (`workflow_dispatch`, input `tag`), never on a push or a tag. The rolling `preview` and any non-numbered tag are refused.
- Its job runs in the GitHub environment `nuget`, whose required reviewer is Stewart. No push starts without his approval in GitHub.
- It publishes the `.nupkg` files `release.yml` attached to that tag, the ones CI built. Before pushing, it checks each file against the release's `SHA256SUMS` and checks that it carries the tag's version. It builds nothing itself.
- It gets its key from `NuGet/login@v1` (permission `id-token: write`), with the nuget.org profile name in the repository variable `NUGET_USER`. It pushes with `--skip-duplicate`, so a rerun after a partial push finishes the job.

## Consequences

- No nuget.org API key exists in the repository, its secrets, a log or a terminal.
- Stewart sets up three things once: on nuget.org, the `Surly.` prefix reservation and a trust policy (owner `StewartScottRogers`, repository `Curl`, workflow `nuget-publish.yml`, environment `nuget`); in GitHub, the `nuget` environment with himself as required reviewer; and the `NUGET_USER` variable.
- The workflow runs only from the default branch, so it can be used once it reaches `master`.
- The hold stands: no run of it is made until Stewart lifts the nuget.org hold (BL-2393).
