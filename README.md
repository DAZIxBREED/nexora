# Nexora Platform

## Overview

**Nexora** is a modular, cross-platform media orchestration platform for Unity and VRChat. Rather than being a single media player, Nexora provides a synchronized runtime for media playback, networking, permissions, automation, playlists, diagnostics, and first-party integrations through independently versioned packages.

Nexora is designed around one shared codebase and a true multi-package architecture. Developers can install only the packages they need while every subsystem communicates through stable public contracts.

## Platform targets

### Runtime compatibility

Nexora is being engineered for synchronized experiences across:

- **PCVR**
- **Meta Quest**
- **Android**
- **iOS**
- **Windows desktop**
- **Linux through the supported Unity development workflow and VRChat's PC runtime path**

The synchronization, permissions, playlists, automation, diagnostics, and integration layers are designed to remain shared across platforms. Rendering, codecs, media backends, shaders, memory budgets, and presentation can be selected per platform without splitting Nexora into separate products.

### Unity development compatibility

The same Nexora repository is intended to open, compile, validate, and package from Unity on both **Windows and Linux**.

Repository rules include:

- Unity `2022.3.22f1` project targeting
- case-sensitive path validation
- consistent source line endings
- no hard-coded Windows or Linux machine paths
- no registry-dependent core tooling
- PowerShell and Bash validation scripts
- package boundaries that behave consistently on both filesystems

## Core design goals

- Build one modular platform for PCVR, Quest, Android, and iOS.
- Preserve synchronized participation across desktop and mobile clients.
- Maintain one authoritative state for playback and automation.
- Keep packages independently versioned and replaceable.
- Allow third-party systems to integrate without modifying Nexora Core.
- Scale from mobile hardware to high-end PCVR environments.
- Support Unity authoring and package generation on both Windows and Linux.

## Package ecosystem

### `com.nexora.core`

Required foundation for lifecycle, shared primitives, module registration, platform identity, and event contracts.

### `com.nexora.api`

Stable extension surface for first-party and third-party modules. Integrations consume public contracts rather than package internals.

### `com.nexora.sync`

Authoritative synchronization layer providing:

- server-time playback reconstruction
- state snapshots
- late-join recovery
- revision tracking
- drift correction
- ownership transfer
- authority heartbeat and failover
- deterministic media state propagation

### `com.nexora.permissions`

Role-based access and control locking for:

- owner
- instance master
- moderator
- DJ
- trusted user
- guest

This package manages authorization, UI locking, administrative access, and rejected-command handling.

### `com.nexora.playlists`

Modular playlist and queue management including:

- synchronized selection
- next and previous navigation
- repeat-current behavior
- repeat-playlist behavior
- playlist events
- future queue, history, scheduling, and content-library support

### `com.nexora.video`

Backend-neutral video control layer for:

- playback
- pause and resume
- seeking
- looping
- volume
- backend replacement
- platform-specific video implementations
- future Unity Video, AVPro, ProTV, and iOS-compatible adapters

Nexora keeps video backends separate from synchronized state so a platform-specific player can be replaced without rewriting networking, permissions, playlists, or automation.

### `com.nexora.audio`

Audio control and routing foundation for:

- gain and mute
- output selection
- future normalization and limiting
- metering
- platform-specific audio presentation
- media and automation integration

### `com.nexora.streaming`

Live-media management for:

- stream health
- reconnect behavior
- retry policies
- live-state handling
- provider abstraction
- backend-independent streaming control

### `com.nexora.spectra`

First-party integration for **SpectraOverdrive**.

Capabilities include:

- authoritative media timecode
- playback-state forwarding
- timeline seeking
- cue dispatch
- show triggering
- lighting automation
- local timeline correction without creating duplicate network authority

SpectraOverdrive owns its fixture and show behavior while Nexora remains the media and timing authority.

### `com.nexora.diagnostics`

Diagnostics and operational visibility for:

- runtime logging
- synchronization revisions
- rejected commands
- authority changes
- stream health
- module events
- platform validation
- future debug dashboards and performance telemetry

## Cross-platform behavior

### PCVR

PCVR receives the complete Nexora experience where supported, including high-resolution media, richer presentation, advanced diagnostics, large playlists, multiple backends, and full SpectraOverdrive integration.

### Meta Quest

Quest uses the shared Nexora state and permission model with a mobile-focused presentation layer. The platform is designed for reduced memory use, lightweight UI, efficient synchronization, mobile-safe shaders, and appropriate video backends.

### Android

Android uses the same mobile-capable architecture as Quest while allowing device-specific layouts and performance profiles. Android clients remain synchronized with PCVR and other supported clients.

### iOS

iOS compatibility is a primary architectural requirement. Nexora isolates media playback from orchestration so iOS-compatible video implementations, codecs, and fallback behavior can be introduced without changing synchronization, playlists, permissions, automation, or SpectraOverdrive timing.

The iOS target includes:

- synchronized playback state
- shared playlists
- role and lock enforcement
- automation and cue compatibility
- SpectraOverdrive timecode integration
- platform-specific media backend support
- graceful handling of unsupported media formats

## One-source-of-truth model

Nexora maintains one authoritative synchronized state. Player backends, UI components, playlists, diagnostics, and integrations consume that state rather than synchronizing independently.

This design prevents:

- competing timestamps
- ownership races
- duplicated network traffic
- conflicting play and pause states
- integration modules becoming accidental playback authorities

## Module and integration system

External systems can subscribe to Nexora events for:

- initialization
- media changes
- playback changes
- time updates
- volume and loop changes
- permission and lock changes
- authority changes
- playlist changes
- platform changes
- diagnostics
- custom cues

New integrations can be added without modifying Nexora Core. Planned possibilities include AudioLink, VRSL, LTCGI, subtitles, DJ control systems, audience interaction, scene automation, request systems, and additional show-control platforms.

## Performance philosophy

Nexora is designed to scale from mobile devices to enthusiast PCVR systems through:

- low-allocation runtime code
- compact synchronized state
- server-time reconstruction instead of constant timestamp replication
- local module time updates
- platform-specific objects and materials
- backend abstraction
- reduced mobile memory and shader requirements
- optional packages rather than one monolithic install

## Long-term vision

Nexora is intended to become a complete media, synchronization, and automation platform for Unity and VRChat. Its package ecosystem will support media players, live streams, playlists, show control, lighting, permissions, diagnostics, creator tools, testing utilities, and third-party modules while keeping **PCVR, Meta Quest, Android, and iOS compatibility** central to every architectural decision.

## Current status

Nexora is in early alpha development. The repository currently establishes the true multi-package architecture and initial runtime contracts. Compile validation, production media adapters, regression scenes, mobile performance testing, and full platform certification remain active development work.
