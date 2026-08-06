# Nexora Architecture

## Package boundaries

`com.nexora.core` contains lifecycle and event distribution only.

`com.nexora.api` contains stable public constants and extension contracts.

`com.nexora.sync` owns authoritative replicated media state and server-time reconstruction.

`com.nexora.permissions` owns authorization and synchronized control locking.

`com.nexora.playlists` owns catalog selection and navigation.

`com.nexora.video`, `com.nexora.audio`, and `com.nexora.streaming` own their respective output domains.

`com.nexora.spectra` adapts Nexora state to SpectraOverdrive.

`com.nexora.diagnostics` provides local observability and future editor validation.

## Dependency policy

Feature packages may depend on Core and API. Integrations may depend on public feature packages, but packages must not access another package's private implementation. Synchronization remains the only network source of playback truth.
