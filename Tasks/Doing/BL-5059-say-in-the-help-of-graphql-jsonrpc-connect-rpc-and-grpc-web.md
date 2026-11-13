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
completed:
---
# BL-5059 — Say in the help of --graphql, --jsonrpc, --connect-rpc and --grpc-web that they cannot be combined

## Goal

The help of --graphql, --jsonrpc, --connect-rpc and --grpc-web each say they cannot be combined with the other three.

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [x] Each of the four options' help text names the other three as not combinable (see the four extension option files).

## Notes

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: fbecace0.
