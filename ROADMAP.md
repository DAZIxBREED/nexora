# Nexora Roadmap to 1.0

This roadmap is the authoritative development contract for Nexora.

## Non-negotiable implementation rule: NO STUBS

A Nexora feature is **not implemented** merely because a package, manifest, class, interface, method, event, enum, inspector field, sample, TODO, simulated response, or documentation entry exists.

A feature may be marked complete only when its complete runtime path exists and the milestone exit gate is satisfied.

The following do **not** qualify as completed functionality:

- empty or placeholder methods
- interfaces without a working implementation
- package shells with no real runtime behavior
- simulated success paths standing in for actual platform behavior
- TODO/FIXME implementations counted as done
- no-op error/recovery handlers
- UI-only enforcement for permissions that can be bypassed in code
- platform labels without a concrete platform runtime path
- documentation claims that exceed implemented behavior

Version numbers advance only after the required runtime behavior has been implemented. Compile/runtime certification is tracked separately and must not be misrepresented.

---

# 0.2 — Core Runtime Completion

## 0.2.6 — Permissions & Command Security

Implement:

- Owner / Master / Moderator / DJ / Trusted / Guest roles
- command-path authorization
- synchronized lock state
- locked UI groups
- role-aware controls
- unauthorized command rejection
- audit events and policy telemetry
- authority-sensitive admin actions
- protected playlist operations
- protected stream operations
- protected automation actions

**Exit gate:** manually firing Udon events or calling public command methods cannot bypass Nexora permissions.

## 0.2.7 — Playlist Engine

Implement:

- multiple playlists
- queue insertion/removal/reorder
- history
- previous/next
- repeat one / repeat playlist
- shuffle
- request queue
- failed-entry quarantine
- retry budgets
- automatic skip
- playlist metadata
- synchronized queue revisions
- late-join reconstruction

**Exit gate:** a late-joining client reconstructs the same active playlist, queue, history-relevant state, and current item as existing clients.

## 0.2.8 — Streaming Engine

Implement:

- live-source detection
- stream lifecycle
- startup buffering state
- stream health state
- reconnect
- bounded exponential backoff
- stall detection
- live-edge handling
- backend fallback
- source failure classification
- recovery coordination
- latency telemetry
- long-running stream support

**Exit gate:** a stream can fail, recover, and resume without destroying or forking synchronized Nexora state.

## 0.2.9 — Diagnostics & Operator Health

Implement:

- structured event history
- synchronization health
- drift telemetry
- authority health
- stream health
- backend health
- playlist health
- permission audit history
- recovery counters
- fault severity
- operator-readable errors
- health snapshots
- diagnostic UI hooks

**Exit gate:** when a supported subsystem fails, Nexora exposes enough state to identify what failed, where, and the recovery state without guessing.

---

# 0.3 — Cross-Platform Certification

## 0.3.0 — Windows PCVR

Certify:

- Unity authoring on Windows
- VRChat PC build
- Unity Video path
- AVPro path where supported
- playlists
- streaming
- synchronization
- authority migration
- Spectra hooks
- long-session playback

**Exit gate:** stable Windows PCVR reference build.

## 0.3.1 — Quest / Android

Implement and optimize:

- Quest backend behavior
- Android playback rules
- HTTPS enforcement
- mobile codec capability detection
- low-allocation paths
- reduced polling
- mobile memory limits
- Quest/mobile UI mode
- mobile fallback behavior
- PCVR ↔ Quest/Android synchronization

**Exit gate:** mixed PCVR/Quest sessions remain synchronized under playback changes, seeks, late joins, and authority transfer.

## 0.3.2 — iOS

Implement:

- iOS-compatible video backend path
- iOS URL/codec capability reporting
- lifecycle recovery
- fallback handling
- platform-specific restrictions
- iOS diagnostics
- synchronized playback with PCVR/Quest/Android

**Exit gate:** iOS participates in the same authoritative Nexora timeline without creating an alternate synchronization authority.

## 0.3.3 — Mixed-Platform Sessions

Validate PCVR + Quest + Android + iOS together for:

- late joins
- master changes
- owner changes
- seeks
- pause/resume
- stream recovery
- playlist changes
- permission locks
- Spectra timecode

**Exit gate:** all supported platforms consume one authoritative session state and recover consistently.

## 0.3.4 — Linux Development Certification

Certify Unity development from Linux:

- clone
- package import
- case-sensitive paths
- validation scripts
- package generation
- editor compatibility
- repository tooling

Linux VRChat runtime support is treated separately from Linux authoring.

**Exit gate:** the same repository and Unity project can be developed from Windows and Linux without maintaining divergent source branches.

---

# 0.4 — Integrations & Automation

## 0.4.0 — SpectraOverdrive

Implement:

- authoritative timecode
- playback state
- seek events
- cue events
- track changes
- playlist changes
- stream health
- show pause/resume
- late-join show reconstruction
- lighting timeline synchronization

**Exit gate:** SpectraOverdrive follows Nexora state and time without owning or competing with media synchronization.

## 0.4.1 — Cue Engine

Implement:

- cue IDs and payloads
- cue banks
- local cues
- synchronized cues
- delayed cues
- conditional cues
- operator cues

**Exit gate:** cues execute deterministically according to their configured local/network scope.

## 0.4.2 — Timeline Automation

Implement:

- timeline events
- media-relative triggers
- absolute-time triggers
- playback-position triggers
- start/end events
- seek recovery
- pause awareness

**Exit gate:** automation remains correct after pause, resume, seek, and late join.

## 0.4.3 — Trigger System

Support:

- buttons
- Udon events
- player entry/exit
- media start/stop
- stream health
- playlist events
- permission state
- external integrations

**Exit gate:** trigger execution obeys permissions, synchronization scope, and repeat policy consistently.

## 0.4.4 — VRChat Ecosystem Integrations

Add optional integration packages where practical for:

- AudioLink
- VRSL
- LTCGI
- ProTV
- other compatible Udon/media systems

**Exit gate:** integrations remain optional and do not introduce hard dependencies into `com.nexora.core`.

---

# 0.5 — Audio

## 0.5.0 — Audio Core

Implement:

- master gain
- mute
- channel gain
- backend audio routing
- media/audio synchronization
- output groups

**Exit gate:** audio state is consistently applied across supported playback backends.

## 0.5.1 — Advanced Audio

Implement:

- meters
- ducking
- normalization hooks
- spatial routing
- channel state
- audio diagnostics

**Exit gate:** advanced audio behavior is observable and does not break synchronization or mobile constraints.

## 0.5.2 — DJ Integration Foundation

Provide working integration contracts for:

- CUT//6
- external DJ systems
- BPM
- beat events
- deck metadata
- track metadata
- media handoff

Nexora must not require CUT//6 as a core dependency.

**Exit gate:** an external DJ system can drive supported Nexora integration points without editing Nexora source.

---

# 0.6 — Nexora SDK

## 0.6.0 — Public API Freeze Candidate

Stabilize public APIs for:

- modules
- events
- media state
- synchronization
- permissions
- playlists
- diagnostics
- automation

**Exit gate:** public API surface is documented and versioned with explicit compatibility expectations.

## 0.6.1 — Module SDK

Provide working:

- module lifecycle
- dependency checks
- capability discovery
- event subscription/dispatch
- error reporting
- extension registration

**Exit gate:** a module can initialize, consume state/events, report faults, and unload/disable without modifying core source.

## 0.6.2 — Third-Party Package Model

Define and enforce `com.vendor.nexora.extension` package rules:

- dependency rules
- compatibility metadata
- API version requirements
- naming/version conventions

**Exit gate:** incompatible modules fail clearly rather than silently corrupting state.

## 0.6.3 — Complete Sample Extensions

Ship fully working examples:

- event listener
- custom playlist integration
- world automation integration
- external display
- custom diagnostics module

**Exit gate:** samples run as documented and contain no placeholder implementation paths.

---

# 0.7 — UI & Operator Experience

## 0.7.0 — Player UI

Guest-facing:

- status
- media title
- time
- volume
- queue
- requests

## 0.7.1 — DJ UI

Add:

- queue management
- URL load
- playlists
- stream controls
- cues
- Spectra controls

## 0.7.2 — Moderator UI

Add:

- lock controls
- user permissions
- backend recovery
- skip failed media
- authority tools

## 0.7.3 — Owner / Master Console

Add:

- full system state
- authority takeover
- module management
- diagnostics
- configuration
- emergency reset

## 0.7.4 — Mobile UI

Provide dedicated Quest/Android/iOS layouts with:

- reduced clutter
- larger controls
- lower update frequency where required
- mobile-safe diagnostics

**0.7 exit gate:** UI permissions exactly mirror command-path permissions. The UI must never be the security boundary.

---

# 0.8 — Reliability & Performance

## 0.8.0 — Network Stress

Test:

- 40+ players where practical
- repeated joins/leaves
- repeated ownership changes
- master migration
- command spam
- rapid seek spam

## 0.8.1 — Media Stress

Test:

- 6+ hour videos
- 6+ hour streams
- repeated stream failures
- backend fallback
- corrupt URLs
- unsupported formats
- rapid playlist transitions

## 0.8.2 — Mobile Stress

Test:

- Quest memory pressure
- Android thermal behavior
- iOS lifecycle changes where applicable
- background/foreground transitions where applicable
- reduced-performance modes

## 0.8.3 — Long-Running Instance Test

Target a 12-hour world session including:

- multiple authority changes
- playlist changes
- stream reconnects
- Spectra running continuously

**0.8 exit gate:** no runaway allocations, retry storms, authority loops, stale-state resurrection, or progressive sync degradation.

---

# 0.9 — Release Candidate

## 0.9.0 — Feature Freeze

No new systems. Only bugs, performance, compatibility, documentation, migration, and tests.

## 0.9.1 — API Freeze

Freeze:

- public namespaces
- package IDs
- SDK contracts
- synchronization contracts
- module lifecycle

## 0.9.2 — Platform Certification

Final certification matrix:

| Platform | Video | Streaming | Sync | Playlist | Permissions | Spectra |
|---|---:|---:|---:|---:|---:|---:|
| Windows PCVR | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Quest | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Android | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| iOS | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

If a platform or VRChat limitation prevents a capability, Nexora must report it explicitly rather than silently fail.

## 0.9.3 — Documentation Freeze

Complete:

- install guide
- upgrade guide
- architecture
- package dependency graph
- module development guide
- platform compatibility guide
- troubleshooting
- diagnostics guide
- API documentation

## 0.9.4 — Migration & Recovery

Support documented migration from:

- Beowulf-era architecture where practical
- Nexora 0.2.x
- Nexora 0.3.x+
- old package layouts

**0.9 exit gate:** release candidate has a frozen API, complete migration path, validated platform matrix, and no known release-blocking defects.

---

# 1.0.0 — Nexora Stable

Nexora 1.0 requires all of the following to be real, implemented, and validated:

- true multi-package architecture
- real video playback
- real livestream handling
- authoritative synchronization
- late-join recovery
- authority failover
- hardened permissions
- complete playlist engine
- diagnostics
- SpectraOverdrive integration
- automation
- audio subsystem
- SDK
- third-party modules
- operator UI
- PCVR support
- Quest support
- Android support
- iOS support
- Windows Unity development
- Linux Unity development
- network stress validation
- long-session reliability
- stable public API
- complete documentation

**1.0 exit gate:** all release-blocking milestone gates above are satisfied, supported platform limitations are documented, and no package or advertised feature relies on a stub or placeholder runtime path.

---

# Version Sequence

```text
0.2.6  Permissions & command security
0.2.7  Playlist engine
0.2.8  Streaming engine
0.2.9  Diagnostics & operator health

0.3.0  Windows PCVR
0.3.1  Quest / Android
0.3.2  iOS
0.3.3  Mixed-platform sessions
0.3.4  Linux development certification

0.4.0  SpectraOverdrive
0.4.1  Cue engine
0.4.2  Timeline automation
0.4.3  Trigger system
0.4.4  VRChat ecosystem integrations

0.5.0  Audio core
0.5.1  Advanced audio
0.5.2  DJ integration foundation

0.6.0  Public API freeze candidate
0.6.1  Module SDK
0.6.2  Third-party package model
0.6.3  Complete sample extensions

0.7.0  Player UI
0.7.1  DJ UI
0.7.2  Moderator UI
0.7.3  Owner / Master console
0.7.4  Mobile UI

0.8.0  Network stress
0.8.1  Media stress
0.8.2  Mobile stress
0.8.3  Long-running instance test

0.9.0  Feature freeze
0.9.1  API freeze
0.9.2  Platform certification
0.9.3  Documentation freeze
0.9.4  Migration & recovery

1.0.0  Stable release
```

This roadmap may be refined as platform realities are discovered, but milestone scope may not be silently reduced to placeholders. If a requirement must change, the change must be explicit in this file and must preserve the no-stubs implementation policy.