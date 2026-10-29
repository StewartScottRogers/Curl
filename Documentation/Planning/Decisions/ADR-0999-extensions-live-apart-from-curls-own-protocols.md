# ADR-0999: Extensions live apart from curl's own protocols

Status: Accepted. Decided by Claude under Stewart's delegation; Stewart asked for the clean separation
and approved this structure ("go", 2026-10-29).

## Context

Curl is a drop-in replacement for curl: same options, exit codes and bytes. Stewart now wants Curl to go
further than upstream - new schemes such as gRPC, CoAP and `s3://`, and new features such as
Server-Sent Events, a local HTTP cache and OAuth 2.0 flows - without blurring what is curl's and what is
Curl's own. Today every protocol is a `Curl.Protocol.<Name>.UnitLibrary` that `Curl.Engine`
references and `EngineComposition.CreateProtocolHandlers` lists by hand, so a new scheme added the same
way would be indistinguishable from curl's and would reach `Libcurl`, whose `curl_version_info` must
match real libcurl's.

## Decision

1. **Names.** What Curl adds beyond upstream curl is an *extension*. Each is
   `Curl.Extension.<Name>.UnitLibrary` with `Curl.Extension.<Name>.UnitTests` (and
   `.IntegrationTests` when it needs a real server), in the `/Extensions/<Name>/` solution folder.
   `Curl.Extension.Composition.UnitLibrary` lists them; `Curl.Extension.Abstractions.UnitLibrary`, made
   when the first extension needs it, holds the hooks beyond `IProtocolHandler` (option rows, request
   and response hooks, `-w` variables, `--ai-help` sections). Disk stays flat.
2. **One-way dependency.** Only the hosts (`Curl.Console`, `Curl.Api`, `Curl.Mcp` and their test
   projects) and the extensions reference an extension. An extension references
   `Curl.Protocol.Abstractions` and the shared layers (wire, security, networking), never a
   `Curl.Protocol.*` library and never another extension but `Curl.Extension.Abstractions`.
   `Directory.Build.props`' `VerifyExtensionIsolation` target fails the build, naming the project,
   when any of the three rules is broken.
3. **The seam.** `EngineComposition.CreateProtocolHandlers` takes an optional `extensionHandlers`
   factory over `ProtocolHandlerServices` (in `Curl.Protocol.Abstractions`): the engine's own
   recording connectors, TLS provider and resolver, so an extension's scheme gets `%{remote_ip}` and the
   like for free and never constructs a socket. Its handlers go after curl's; a scheme both claim makes
   `ProtocolDispatcher` throw, so an extension can never replace curl's handler.
4. **Registration.** `ExtensionCatalogue.Entries` holds one factory per line, plain constructor calls
   (native AOT, no reflection). It is `merge=union` and in `Tasks/SharedListFiles.txt`, so lanes adding
   different extensions do not overlap.
5. **Drop-in safety.** Extensions are on by default: a new scheme or option is something real curl
   refuses with exit 1 or 2, so no working script depends on it. `CURL_EXTENSIONS=0` turns them all
   off. `curl -V` does not change; extensions are listed by a later `--extensions` option and
   `--ai-help extensions`. `Libcurl` passes no extensions. If a later upstream curl adds an option or
   scheme an extension uses, upstream's wins and the extension is renamed.
6. **Verification.** The gap office and the audits' conformance checks leave extensions out: there is
   no curl to compare them with. An extension is tested against its specification (RFC test vectors)
   and, in its `.IntegrationTests`, against a reference implementation. The four quality gates apply.

`Curl.Console` passes the extensions in both production compositions (`CreateTransferDispatch` and
the dialing runner). `Curl.Api` and `Curl.Mcp` drive `ManagedCurlEngine`, which builds its own
handlers; giving them extensions is later work.

## Consequences

Curl's own protocols and their behaviour are untouched. A lane can build any extension in its own
projects without touching a file another extension touches, except one line in `ExtensionCatalogue`.
`--ai-help` and `Curl.Mcp` are extensions in spirit; they stay where they are, and the extensions
document says so. `Curl.HttpMessageSignatures` is not an extension: curl 8.22.0 has `--httpsig-*`.
