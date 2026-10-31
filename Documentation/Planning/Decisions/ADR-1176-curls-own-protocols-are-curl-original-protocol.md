# ADR-1176: Curl's own protocols are named Curl.Original.Protocol.<Name>

- Status: Accepted
- Date: 2026-10-31
- Asked for by Stewart (2026-10-31: rename `Curl.Protocol.*` to `Curl.Original.Protocol.*`, keep
  `Curl.Protocol.Abstractions`, solution folders `/Original/Protocols/<Name>/`); carried out in BL-4439

## Context

ADR-0999 keeps what Curl adds beyond upstream curl apart from curl's own protocols, and ADR-1174 named the
extensions that serve their own scheme `Curl.Extension.Protocol.<Name>`. Curl's own protocols were
`Curl.Protocol.<Name>`, a name that did not say they are the ones upstream curl has.

## Decision

Each protocol upstream curl has is `Curl.Original.Protocol.<Name>.UnitLibrary` (with `.UnitTests` and, for
SSH, `.IntegrationTests`), namespace `Curl.Original.Protocol.<Name>`, in `/Original/Protocols/<Name>/` in
`Curl.slnx`: Dict, File, Ftp, Gopher, Http, Imap, Ldap, Mqtt, Pop3, Rtsp, Smb, Smtp, Ssh, Telnet, Tftp and
Ws.

`Curl.Protocol.Abstractions` keeps its name and its `/Protocols/Abstractions/` folder: the extensions build
on it as much as curl's protocols do, so "Original" would be a false name for it.

## Consequences

- The two checks that find protocols by name follow it: `VerifyExtensionIsolation` in
  `Directory.Build.props` refuses an extension's reference to a `Curl.Original.Protocol.*` project, and
  `ProtocolIsolationTests` reads the `Curl.Original.Protocol.*.UnitLibrary` folders.
- Inside the `Curl.Original.Protocol` namespace a partly qualified `Abstractions.X` or
  `Protocol.Abstractions.X` no longer reaches `Curl.Protocol.Abstractions`, so those references are
  written in full.
- The audit and gap offices' tools and instructions that name a protocol project change through their own
  branches. Task files in `Tasks/Done` and audit and gap findings keep the old names as history.
