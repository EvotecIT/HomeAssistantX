# Roadmap

HomeAssistantX should remain one dependency-light package. New packages are justified only by a real platform dependency, such as an mDNS implementation that cannot stay portable and dependency-free.

## Before the first stable release

- [ ] Add WebSocket `supported_features` negotiation and bounded decoding for coalesced message arrays.
- [ ] Decide and document the authentication recovery policy for an unexpected HTTP 401 or WebSocket `auth_invalid`; avoid hidden infinite retries.
- [ ] Add API compatibility baselines once version 0.1.0 becomes the published contract.
- [ ] Run the read-only live suite against representative current and older supported Home Assistant releases before defining a minimum server version.

## Next adapters, driven by consumers

- [ ] Add instance discovery only after choosing a cross-platform mDNS boundary that does not burden every package consumer.
- [ ] Add a native-app registration/webhook adapter when CasaRay or another real host needs companion-app lifecycle behavior.
- [ ] Promote integration-specific raw commands into typed APIs only when two consumers need the same stable contract.
- [ ] Add an MCP/agent adapter over the same explicit connection and typed operations after the library and PowerShell contracts have field experience. Keep mutations policy-gated and expose read-only troubleshooting first.
- [ ] Add optional recorder/statistics, energy-preference, and media-browser models when real consumers need more than the bounded raw APIs.
- [ ] Add narrowly scoped restore/recovery helpers only after defining interactive confirmation, backup verification, and failure-recovery contracts.

## Explicit non-goals

- Reimplementing Home Assistant entity domains as a second source of truth.
- Owning application credential storage or UI authorization callbacks.
- Folding HomeKit or product-specific normalized device models into the Core package.
- Generating one PowerShell cmdlet for every Home Assistant domain, action, integration, or app.
