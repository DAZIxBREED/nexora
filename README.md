# Nexora

Nexora is a true multi-package Unity and VRChat media orchestration platform.

## Package ecosystem

- `com.nexora.core`
- `com.nexora.api`
- `com.nexora.sync`
- `com.nexora.permissions`
- `com.nexora.playlists`
- `com.nexora.video`
- `com.nexora.audio`
- `com.nexora.streaming`
- `com.nexora.spectra`
- `com.nexora.diagnostics`

## Design rules

Nexora Core owns lifecycle and shared event contracts. Feature packages communicate through public contracts and never reach into another package's internals. Synchronization has one authoritative state. Integrations such as SpectraOverdrive consume that state rather than creating a second source of truth.

Target editor: Unity `2022.3.22f1` on Windows and Linux.
