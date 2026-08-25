# HomeAssistantX contributor guidance

## Ownership

HomeAssistantX owns reusable Home Assistant protocol behavior: authentication, REST and WebSocket transport, command correlation, events, subscriptions, reconnect, reconciliation, generic JSON models, diagnostics, and classified failures.

Consumers own credential persistence, normalized device/domain models, action safety policy, presentation, and product behavior. Do not add CasaRay-, Tactra-, or UI-specific models to this repository.

## Compatibility

- Keep the single package compatible with `net472`, `netstandard2.0`, and `net10.0`.
- Avoid adding dependencies unless they carry a clear protocol or compatibility benefit.
- Preserve unknown Home Assistant JSON fields and offer raw escape hatches for evolving or integration-specific APIs.
- Keep public APIs asynchronous and cancellation-aware.
- Never include access tokens, URLs identifying a private installation, entity inventories, or response payloads from a real home in source, tests, logs, commits, or issues.

## Tests

- Prefer contract-shaped loopback HTTP/WebSocket peers and safe read-only live validation over mock call-count tests.
- Test protocol ordering, framing, concurrency, cancellation, reconnect, and error classification.
- Service-call payloads may be proven against the local peer. Live validation must remain read-only unless a user explicitly authorizes a specific mutation.
- Build every target framework and verify the packed NuGet can be consumed independently of project references.
