# Roadmap

HomeAssistantX remains one dependency-light package. New packages are justified only by a real platform dependency.

## Before the first stable release

- [ ] Run the read-only live suite against representative current and older supported Home Assistant releases before defining a minimum server version.

## Next adapters, driven by consumers

- [ ] Promote integration-specific raw commands into typed APIs only when two consumers need the same stable contract.
- [ ] Add IPv6 mDNS discovery when a real IPv6-only consumer can validate the platform behavior.
- [ ] Publish an optional NaCl SecretBox adapter only when a dependency can be justified, maintained, and validated across all target frameworks; the core protector interface remains dependency-free.
- [ ] Add an MCP/agent adapter over the same explicit connection and typed operations after the library and PowerShell contracts have field experience. Keep mutations policy-gated and expose read-only troubleshooting first.
- [ ] Add narrowly scoped restore/recovery helpers only after defining interactive confirmation, backup verification, and failure-recovery contracts.

## Explicit non-goals

- Mirroring every Home Assistant domain and integration action as static types;
  the runtime action catalog remains the source of truth beyond common controls.
- Owning application credential storage or UI authorization callbacks.
- Folding HomeKit or product-specific normalized device models into the Core package.
- Generating one PowerShell cmdlet for every Home Assistant domain, action, integration, or app.
