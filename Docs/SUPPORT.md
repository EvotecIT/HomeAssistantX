# Home Assistant support matrix

This matrix was checked against the official Home Assistant developer documentation and current frontend contracts on 26 August 2026. “Core coverage” means the documented external [REST API](https://developers.home-assistant.io/docs/api/rest/), [WebSocket API](https://developers.home-assistant.io/docs/api/websocket/), [Authentication API](https://developers.home-assistant.io/docs/auth_api/), and [Conversation API](https://developers.home-assistant.io/docs/intent_conversation_api/). Frontend operational commands and the [Supervisor API](https://developers.home-assistant.io/docs/api/supervisor/endpoints/) are marked separately because their compatibility and privilege boundaries differ.

## Status language

- **First class** — a named .NET API validates the request and decodes stable response fields.
- **Extensible** — a named .NET API owns the transport contract and returns `JsonElement` for deliberately open-ended data.
- **Raw** — available through the bounded, authenticated REST or WebSocket escape hatch, but without a named API.
- **Not supported** — the transport or lifecycle is not implemented and must not be claimed by consumers.

## Authentication

| Capability | Status | Public surface |
| --- | --- | --- |
| Long-lived token supplied by the host | First class | `StaticAccessTokenProvider` |
| OAuth authorization URL and code exchange | First class | `HomeAssistantOAuthClient` |
| OAuth refresh and refresh-token revocation | First class | `HomeAssistantOAuthClient` |
| Serialized proactive refresh and host-owned persistence | First class | `RefreshingAccessTokenProvider` |
| Rejected-token recovery | First class | Refresh-capable providers retry REST once after HTTP 401; WebSocket recovery opens a fresh session after `auth_invalid` |
| Temporary signed paths | First class | `HomeAssistantSystemClient.SignPathAsync` |
| Create a long-lived token | First class | `CreateLongLivedAccessTokenAsync`; sensitive and never used by live tests |
| Secure credential storage | Host-owned | Keychain, Credential Manager, or another platform store implements the provider/persistence callback |
| Custom recovery policy | Extensible | A custom token provider implements `IHomeAssistantAccessTokenRecovery`; static long-lived tokens remain fail-closed |

OAuth tokens are never logged or persisted by HomeAssistantX. A native application still needs an approved redirect URI advertised by its web client identifier.

## REST API

All endpoints currently listed in the official REST API reference have named methods.

| Endpoint family | Status | Notes |
| --- | --- | --- |
| API status, configuration, components | First class | Stable models preserve unknown fields |
| Event and service/action catalogs | First class / extensible | Action definitions and fields are typed; raw selectors, targets, responses, and unknown integration data are preserved |
| History and logbook | First class | Time ranges, entity filters, and history performance flags are supported |
| State list/read/create/update/delete | First class | State writes alter HA's state representation; they do not control the device |
| Service/action calls | First class / extensible | Fluent entity, device, area, floor, and label targets; optional response data preserved |
| Fire event | Extensible | Named method with open event-data object |
| Camera proxy | First class | Bounded binary response |
| Calendars and calendar events | First class | Timed and all-day boundaries supported |
| Error log | First class | Bounded plaintext response; consumers must treat log content as sensitive |
| Template rendering | First class | Bounded plaintext response and optional variables |
| Configuration check | First class | Typed valid/invalid result |
| Intent handling and conversation | Extensible | Named methods with forward-compatible JSON results |
| Integration-specific REST endpoints | Raw | `SendAsync<T>`, `SendTextAsync`, and `GetBytesAsync` retain authentication, same-origin, timeout, error, and size protections |

## WebSocket API and notifications

| Capability | Status | Notes |
| --- | --- | --- |
| Authentication and standard message frames | First class | Concurrent commands are correlated by identifier; fragmented frames are reassembled |
| Supported-features negotiation | First class | `supported_features` is command ID 1 and enables `coalesce_messages` version 1 by default |
| Ping/pong | First class | `PingAsync` |
| Subscribe/unsubscribe to events | First class | Bounded consumer queue and explicit completion |
| Subscribe to triggers | First class | Open trigger definitions; no polling |
| State snapshot plus live reconciliation | First class | Subscribes before snapshot, buffers races, reconnects, and emits missed changes |
| Fire event and call service/action | First class / extensible | Typed command construction with flexible results |
| Get states, config, services, panels | First class / extensible | Stable data typed where useful; open schemas preserved as JSON |
| Validate config | Extensible | Trigger, condition, and action fragments supported |
| Extract target and get applicable triggers/conditions/services | Extensible | Full HA target shape and `expand_group` supported |
| Entity registry list for display | Extensible | Compact HA response remains JSON to preserve its evolving abbreviated schema |
| List/change voice-assistant exposure | Extensible | Uses documented plural entity and assistant arrays |
| Signed paths and long-lived token creation | First class | Authentication commands with explicit secret boundary |
| Conversation processing | Extensible | REST and WebSocket entry points |
| Other/custom commands | Raw | `RequestAsync` and `SubscribeAsync` |
| Coalesced message batches | First class | Object and array frames are decoded; byte and message-count limits reject oversized batches |

## Operations and troubleshooting

These APIs are grouped under `client.Operations`. Stable fields are typed while
open-ended Home Assistant payloads remain `JsonElement`.

| Capability | Status | Public surface |
| --- | --- | --- |
| Capability discovery | First class | `GetCapabilitiesAsync` reports available, unavailable, and permission-dependent capabilities |
| Structured system log | First class | `Logs.GetSystemLogAsync`; legacy plaintext endpoint remains separate |
| Repairs | First class / extensible | List issues, include ignored issues, get issue data, set ignored state |
| System health | Extensible | Collects the streamed `system_health/info` response into a bounded snapshot |
| Configuration entries | First class / extensible | List/filter/get, reload, enable/disable, start user-initiated reconfiguration, and continue an existing flow |
| Automation and script traces | First class / extensible | List summaries, retrieve one run, inspect related contexts |
| Update entities | First class | Discover update states, release notes, and invoke `update.install` |
| Diagnostics | First class / binary | List diagnostic handlers and download redacted config-entry or device diagnostics |

## Supervisor and Home Assistant OS

`client.Supervisor` supports two transports: the administrator-only Core
frontend proxy for normal remote clients and a separate direct Supervisor
bearer-token client for trusted app/add-on contexts.

| Capability | Status | Notes |
| --- | --- | --- |
| Supervisor and Core information | First class / extensible | `GetInfoAsync` models Supervisor component health/version, `GetOverviewAsync` models the combined installation, and Core-specific data remains extensible |
| Available updates | First class | Core, Supervisor, OS, and installed app targets |
| Apps/add-ons | First class / extensible | List/get plus install, update, start, stop, restart, and uninstall |
| Backups | First class / extensible | List and create full backups, including compression, location, password, database exclusion, and background mode |
| Jobs | First class / extensible | List recent jobs and retrieve one job |
| Resolution issues | Extensible | Supervisor resolution payload preserved |
| Logs | First class | Bounded Core, Supervisor, host, and app plaintext log retrieval |
| Restarts and updates | First class | Explicit typed targets with caller-owned confirmation |
| Raw endpoint | Protected raw | Root-relative paths only, authenticated transport, bounded response, error classification |
| Restore, wipe, recovery, shutdown | Deliberately unmodeled | Destructive recovery operations require an explicitly owned raw call and policy |

Supervisor capabilities require Home Assistant OS or a supervised installation
and suitable permissions. They are unavailable on Container and Core-only
installations. The direct transport uses a separate credential and never
reuses a Core bearer token against another origin.

## PowerShell

The binary module supports Windows PowerShell 5.1 and PowerShell 7. It exposes
task-level cmdlets over the .NET engine, a runspace-local default plus explicit
pipeline-capable connections, joined inventory discovery, typed controls,
target-oriented parameter sets, WebSocket event streaming, and `ShouldProcess`
for mutations. See [POWERSHELL.md](POWERSHELL.md).

## Registries and platform boundaries

`Registries.GetSnapshotAsync` provides raw typed area, floor, device, entity, and
config-entry data. `Inventory.GetSnapshotAsync` joins those registries with live
states and the runtime action catalog. It applies Home Assistant's entity-area
fallback, resolves friendly names/aliases or native IDs, rejects ambiguous
matches, and retains raw objects for forward compatibility.

`Controls` provides typed standard calls for lights, switches, climate, covers,
media players, and locks. These are validated service builders, not a second
product device model; runtime action discovery and the generic service client
remain available for every integration-defined action.

Most registry commands are frontend-facing Home Assistant commands rather than
the stable external API documented alongside the Core WebSocket commands. They
are therefore compatibility-sensitive even though loopback and live tests cover
them.

The following boundaries are intentional:

| Area | Status | Reason |
| --- | --- | --- |
| Instance discovery over mDNS | Not supported yet | Cross-platform discovery deserves an optional adapter or a dependency-free implementation; manual URL entry works today |
| Native companion-app registration and webhook lifecycle | Raw / future adapter | Different lifecycle from the Core client; should not distort the base transport |
| HACS/custom repository package installation | Not modeled | Not a stable Core package-management contract; Supervisor apps are supported separately |
| Energy preferences and media browsers | Raw | Schemas evolve independently; consumers can use protected raw commands until a reusable model earns ownership here |
| Product device-domain normalization | Consumer-owned | CasaRay/Tactra map joined HA inventory into their own product models and safety/UI policy |
| HomeKit protocol/bridge | Out of scope | Separate protocol and credential model; not part of HomeAssistantX |

## Evidence

The normal contract suite runs against a real loopback HTTP/WebSocket peer, including rejected-token recovery, exactly-once retry, fragmented and coalesced frames, malformed and oversized batch rejection, out-of-order responses, bounded bodies, OAuth refresh concurrency, cancellation, subscription failure, reconnect, and missed-state reconciliation. It runs on .NET Framework 4.7.2 and .NET 10.

The optional live suite is read-only. It validates the configured real instance's API status, configuration, components, event and service catalogs, state REST/WebSocket parity, panels, registries, signed paths, subscription setup, recent history when the recorder is loaded, operational capability discovery, system logs, Repairs, diagnostics handlers, configuration entries, update discovery, and accessible Supervisor inventory. It does not call services, fire events, create tokens, install updates, restart components, create backups, expose entities, or otherwise mutate the home.
