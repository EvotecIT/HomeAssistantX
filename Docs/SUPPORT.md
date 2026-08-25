# Home Assistant support matrix

This matrix was checked against the official Home Assistant developer documentation on 25 August 2026. “Core coverage” means the documented external [REST API](https://developers.home-assistant.io/docs/api/rest/), [WebSocket API](https://developers.home-assistant.io/docs/api/websocket/), [Authentication API](https://developers.home-assistant.io/docs/auth_api/), and [Conversation API](https://developers.home-assistant.io/docs/intent_conversation_api/). It does not mean every private frontend command, custom integration endpoint, Supervisor endpoint, or device-domain model.

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
| Temporary signed paths | First class | `HomeAssistantSystemClient.SignPathAsync` |
| Create a long-lived token | First class | `CreateLongLivedAccessTokenAsync`; sensitive and never used by live tests |
| Secure credential storage | Host-owned | Keychain, Credential Manager, or another platform store implements the provider/persistence callback |
| Automatic retry after an unexpected HTTP 401 | Not supported | Authentication failure is surfaced; the host decides whether to refresh or reauthorize |

OAuth tokens are never logged or persisted by HomeAssistantX. A native application still needs an approved redirect URI advertised by its web client identifier.

## REST API

All endpoints currently listed in the official REST API reference have named methods.

| Endpoint family | Status | Notes |
| --- | --- | --- |
| API status, configuration, components | First class | Stable models preserve unknown fields |
| Event and service/action catalogs | First class / extensible | Event summary is typed; service schemas remain JSON because integrations define them |
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
| Coalesced message feature | Not supported | Do not enable `coalesce_messages`; array-frame decoding and negotiation are still required |

## Registries and platform boundaries

`Registries.GetSnapshotAsync` provides typed area, floor, device, entity, and config-entry data used by CasaRay-class consumers. Most of those registry commands are frontend-facing Home Assistant commands rather than the stable external API documented alongside the Core WebSocket commands. They are therefore compatibility-sensitive even though they are covered by loopback and live tests.

The following boundaries are intentional:

| Area | Status | Reason |
| --- | --- | --- |
| Instance discovery over mDNS | Not supported yet | Cross-platform discovery deserves an optional adapter or a dependency-free implementation; manual URL entry works today |
| Native companion-app registration and webhook lifecycle | Raw / future adapter | Different lifecycle from the Core client; should not distort the base transport |
| Supervisor and Home Assistant OS administration APIs | Out of scope | Different privilege and deployment boundary |
| Custom integrations, energy preferences, media browsers, backups | Raw | Schemas evolve independently; consumers can use protected raw commands until a reusable model earns ownership here |
| Device-domain normalization | Consumer-owned | CasaRay/Tactra map raw HA state into their own normalized product models |
| HomeKit protocol/bridge | Out of scope | Separate protocol and credential model; not part of HomeAssistantX |

## Evidence

The normal contract suite runs against a real loopback HTTP/WebSocket peer, including authentication, fragmented frames, out-of-order responses, bounded bodies, OAuth refresh concurrency, subscription failure, reconnect, and missed-state reconciliation. It runs on .NET Framework 4.7.2 and .NET 10.

The optional live suite is read-only. It validates the configured real instance's API status, configuration, components, event and service catalogs, state REST/WebSocket parity, panels, registries, signed paths, subscription setup, and recent history when the recorder is loaded. It does not call services, fire events, create tokens, expose entities, or otherwise mutate the home.
