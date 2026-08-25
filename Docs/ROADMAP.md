# Roadmap

HomeAssistantX should remain one dependency-light package. New packages are justified only by a real platform dependency, such as an mDNS implementation that cannot stay portable and dependency-free.

## Before the first stable release

- [ ] Add WebSocket `supported_features` negotiation and bounded decoding for coalesced message arrays.
- [ ] Decide and document the authentication recovery policy for an unexpected HTTP 401 or WebSocket `auth_invalid`; avoid hidden infinite retries.
- [ ] Add package-consumer smoke projects for .NET Framework 4.7.2, .NET Standard 2.0 compatibility, and .NET 10 using the packed `.nupkg` rather than project references.
- [ ] Add API compatibility baselines once version 0.1.0 becomes the published contract.
- [ ] Run the read-only live suite against representative current and older supported Home Assistant releases before defining a minimum server version.

## Next adapters, driven by consumers

- [ ] Add instance discovery only after choosing a cross-platform mDNS boundary that does not burden every package consumer.
- [ ] Add a native-app registration/webhook adapter when CasaRay or another real host needs companion-app lifecycle behavior.
- [ ] Promote integration-specific raw commands into typed APIs only when two consumers need the same stable contract.
- [ ] Build a thin PowerShell module over HomeAssistantX after the .NET API settles; do not duplicate transport logic in cmdlets.

## Explicit non-goals

- Reimplementing Home Assistant entity domains as a second source of truth.
- Owning application credential storage or UI authorization callbacks.
- Folding HomeKit, Supervisor administration, or product-specific normalized device models into the Core package.
