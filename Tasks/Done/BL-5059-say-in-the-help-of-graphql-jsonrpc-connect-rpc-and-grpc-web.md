---
id: BL-5059
title: Say in the help of --graphql, --jsonrpc, --connect-rpc and --grpc-web that they cannot be combined
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.GraphQl.UnitLibrary/GraphQlOptions.cs, Curl.Extension.JsonRpc.UnitLibrary/JsonRpcExtension.cs, Curl.Extension.ConnectRpc.UnitLibrary/ConnectRpcExtension.cs, Curl.Extension.GrpcWeb.UnitLibrary/GrpcWebExtension.cs]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-5059 — Say in the help of --graphql, --jsonrpc, --connect-rpc and --grpc-web that they cannot be combined

## Goal

The help of --graphql, --jsonrpc, --connect-rpc and --grpc-web each say they cannot be combined with the other three.

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [x] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-11-12: Doing -> Done. The four extension options' help texts now say they cannot be combined; extension unit tests pass.
