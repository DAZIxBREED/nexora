# Nexora Development Policy

This file defines the engineering rules for Nexora development through 1.0 and beyond.

## 1. No Stubs

Nexora does not count stubs, placeholders, shells, simulated success paths, or documentation-only declarations as implemented functionality.

A feature is implemented only when its complete intended runtime path exists.

Examples that are **not implementation**:

- an empty class or method
- a method that returns a fixed success value without performing the operation
- an interface with no working concrete implementation
- a package containing only manifests, assembly definitions, or documentation
- a backend name with no functioning backend
- an event that is declared but never generated or consumed correctly
- a retry method that does not actually schedule and execute retries
- a diagnostic field that is never populated from runtime behavior
- UI locking without command-path authorization
- a platform flag without a platform-specific runtime path
- a test that only checks that a class exists
- TODO/FIXME code presented as a completed capability

## 2. Complete Runtime Path Rule

For a feature to be marked complete, all required stages must work together.

Example — synchronized playback:

```text
Authorized command
  -> authoritative state mutation
  -> serialization
  -> remote deserialization
  -> snapshot acceptance/rejection
  -> backend application
  -> real playback behavior
  -> backend telemetry
  -> drift/recovery behavior
```

If any required stage is absent, the feature is incomplete.

## 3. Honest Versioning

A version may be created only after the runtime work assigned to that milestone has been implemented.

A release record must distinguish between:

- implemented
- compile-tested
- runtime-tested
- platform-certified

These are not interchangeable claims.

Nexora must never claim compile, runtime, or platform certification without that validation actually having occurred.

## 4. Real Platform Support

Claiming support for PCVR, Quest, Android, or iOS requires a concrete runtime path appropriate to that platform.

Shared architecture may be written in advance, but architecture alone does not count as platform support.

Where VRChat, Unity, the operating system, codecs, or device capabilities impose limitations, Nexora must report the limitation clearly instead of pretending parity exists.

## 5. Security Below the UI

The UI is never the authorization boundary.

Every protected Nexora action must enforce authorization in the runtime command path. Disabling a button alone is insufficient.

This applies to:

- playback
- seeking
- playlists
- streaming
- authority operations
- permissions
- automation
- integrations
- administrative recovery

## 6. One Authoritative Media Truth

Nexora synchronization follows one authoritative media timeline.

Backends, integrations, SpectraOverdrive, UI, diagnostics, playlists, and automation may consume authoritative state but must not silently create competing media authorities.

Rejected stale state must never be allowed to become authoritative again through ownership transfer or local fallback behavior.

## 7. Package Boundaries Are Real

Nexora is a true multi-package platform.

Packages must communicate through documented dependencies and contracts. Circular dependencies must not be introduced to make an implementation easier.

Optional integrations must remain optional. `com.nexora.core` must not acquire hard dependencies on optional systems.

## 8. Failure Paths Must Be Implemented

Happy-path behavior alone is not completion.

Relevant features must implement bounded and observable behavior for conditions such as:

- invalid URLs
- access denial
- unsupported media
- player errors
- stream stalls
- rate limiting
- backend loss
- authority departure
- stale network state
- retry exhaustion
- playlist item failure
- platform capability mismatch

Retries must be bounded. Recovery loops must not run forever.

## 9. Diagnostics Must Reflect Reality

Diagnostic values must be derived from real runtime state.

Counters and status fields should allow an operator to distinguish at least:

- healthy
- waiting/not ready
- degraded
- recovering
- failed
- recovery exhausted

Diagnostics must not claim success merely because no error callback fired.

## 10. Tests Must Exercise Behavior

Tests should validate behavior and state transitions, not only file/class existence.

As Nexora approaches 1.0, milestone validation should include:

- command authorization tests
- serialization/deserialization tests
- late-join tests
- authority migration tests
- stale-state rejection tests
- drift correction tests
- backend failure/recovery tests
- playlist reconstruction tests
- stream recovery tests
- mixed-platform tests
- long-running instance tests

## 11. No Silent Scope Reduction

The authoritative roadmap is `ROADMAP.md`.

If a platform limitation or architectural discovery requires changing a milestone, the roadmap must be changed explicitly. A required feature must not quietly be replaced with a placeholder and then marked complete.

## 12. Definition of Done

A Nexora milestone is done only when:

1. its required runtime behavior is implemented;
2. its exit gate in `ROADMAP.md` is satisfied at the appropriate development stage;
3. package versions and dependencies are consistent;
4. no claimed feature in the milestone is a stub;
5. known limitations are documented honestly;
6. the release record accurately states its validation level.

These rules are part of Nexora's engineering contract and apply to all future development.