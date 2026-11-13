# ADR-1262 — Two body-replacing extension options are refused with exit 2

- Status: Accepted
- Date: 2026-11-12
- Task: BL-5023
- Decided by Claude under Stewart's delegation.

## Context

The outside fitness review (EXT-043) found `--graphql q.graphql --jsonrpc 'a [1]'` sent one POST with the JSON-RPC
body and GraphQL's `Accept` header, exit 0, no warning. ADR-1026 (GraphQL), ADR-1028 (JSON-RPC), Connect RPC and
gRPC-Web each say their option replaces the request body, so two of them cannot both be honoured. The WebDAV pair
(`--webdav-list` with `--webdav-sync`) and two OAuth2 flows (ADR-1075) are already refused.

## Decision

`BodyReplacingHookExclusivity.HookFor` is the first entry of `ExtensionTransferHookCatalogue`. When an option group
gives two of `--graphql` (or `--graphql-introspect`), `--jsonrpc`, `--connect-rpc` and `--grpc-web`, it gives a hook
that fails the transfer with exit 2 (`CurlExitCode.FailedInit`) and `--<first> and --<second> cannot be used together`,
the options in that order, without calling the next handler, so nothing is sent. Giving one of them, or one with a
non-body option such as `--har`, is unchanged. This applies to ADR-1026 and ADR-1028 alike.
