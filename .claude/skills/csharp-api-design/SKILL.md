---
name: csharp-api-design
description: Use when changing the public/published API of a NuGet-packable .NET library — adding, removing, renaming, or re-signing a public type or member; adding a parameter to an already-shipped public method; tightening what a public method accepts or changing its serialised/wire output; marking something [Obsolete]; or reviewing a PR for source, binary, or wire compatibility. Covers extend-only design, the three compatibility types, deprecation, and versioning. Do NOT use for internal-only code or an application/host with no published surface.
---

# Public API Design and Compatibility

Design public library APIs so consumers can upgrade without their code breaking. The core discipline: **once something ships, extend it — never quietly remove or change it.**

## The Three Types of Compatibility

| Type | Definition | What breaks it |
|------|------------|----------------|
| **Source** | Consumer code still compiles against the new version | Removed/renamed members, changed signatures, narrowed visibility |
| **Binary** | Already-compiled consumer assemblies still run | Changed method tokens / assembly layout, optional-param changes, removed members |
| **Wire** | Serialised data stays readable across versions | Renamed fields, changed types, embedded type names |

Source and binary differ: a change can recompile fine yet throw `MissingMethodException` against a pre-built caller. Wire is independent again — it governs persisted data and network messages.

## Extend-Only Design

1. **Shipped functionality is immutable** — behaviour and signatures are locked once released.
2. **New functionality arrives through new constructs** — overloads, new types, opt-in features.
3. **Removal only after a deprecation period** — measured in releases, announced ahead.

The payoff: old code keeps working, old and new paths coexist, and consumers upgrade on their own schedule.

## API Change Guidelines

### Safe in any release

```csharp
// ADD a new overload that delegates to the existing method
public void Process(Order order) { ... }                       // unchanged
public void Process(Order order, CancellationToken ct) { ... } // new overload

// ADD new types, interfaces, enums, and members
public interface IOrderValidator { }
public sealed record OrderShipped(OrderId Id, DateTimeOffset At);
public DateTimeOffset? ShippedAt { get; init; }                // new member
```

### Breaking — never, or a major version only

```csharp
// REMOVE or RENAME a public member
// CHANGE a parameter type/order, return type, or access modifier
// TIGHTEN what an input accepts, or change serialised output shape/behaviour

// ADD an optional parameter to an ALREADY-SHIPPED method — binary break!
// The default is baked into each CALLER assembly at compile time, so pre-built
// callers bind to a method token that no longer exists -> MissingMethodException.
public void Process(Order order, CancellationToken ct = default); // breaks binary compat
// Correct: add a new overload instead (see above).

// ADD a required parameter — breaks every caller.
```

A subtle one: **tightening an accepted input** (rejecting values the old version accepted) or **changing serialised output** is a breaking behavioural change even when the signature is untouched. Signature-snapshot tools will not catch it — behavioural tests will.

### Deprecation pattern

```csharp
[Obsolete("Obsolete since v1.5.0. Use ProcessAsync instead.")]
public void Process(Order order) { }                                    // 1. mark, keep working

public Task ProcessAsync(Order order, CancellationToken ct = default);  // 2. add replacement, same release

// 3. Remove only in the next major version, after consumers have had time to migrate.
```

## Encapsulation

- **Hide implementation detail.** Keep genuinely internal types `internal` (use `InternalsVisibleTo` for tests). The smaller the public surface, the less you are committed to.
- **Sealing is a judgement call, not a default.** Seal leaf types with no inheritance story. But a framework with intended extension points (base entities, generic commands, consumer-implemented interfaces) must leave those open — sealing them defeats the design.
- **Prefer small, focused interfaces.** You cannot add a member to a published interface without breaking every implementor, so split read/write or capability concerns rather than shipping one monolith.

## Wire Compatibility

For persisted data and distributed messages, serialised payloads must round-trip across versions:

| Direction | Requirement |
|-----------|-------------|
| **Backward** | New code reads data written by old code |
| **Forward** | Old code still reads data written by new code |

Both are needed for zero-downtime rolling upgrades. Evolve in phases: ship read-side support for the new shape first, enable writing it later (opt-in), make it the default only once the install base has absorbed the read side.

Formats with **explicit field identifiers** (Protocol Buffers, MessagePack with contracts, System.Text.Json with explicit property names / source generation) evolve more safely than reflection-based formats that **embed CLR type names** in the payload — renaming a type then breaks the wire. Avoid polymorphic serialisation keyed on `$type`; prefer explicit, stable discriminators.

## Versioning

| Bump | Allowed changes |
|------|-----------------|
| **Patch** | Bug fixes, security patches |
| **Minor** | New (additive) features, deprecations |
| **Major** | Breaking changes, removal of deprecated API |

Principles: no surprise breaks (even majors are announced), extensions can ship any time, deprecate before you remove, and apply **Chesterton's Fence** — understand why a public API exists before changing it; assume someone depends on it.

## Optional: API Approval Tests

Tools like **PublicApiGenerator + Verify** snapshot the public surface to a checked-in baseline, so any signature-level change (removal, rename, visibility narrowing, optional-param addition) shows up as a reviewable diff. Recommended for published libraries with external consumers. Caveats:

- The snapshot covers **signatures, not behaviour** — it will not catch a tightened input or changed serialised output. Cover those with behavioural tests.
- The baseline is sensitive to compiler / preview-SDK churn, so pin tool versions and expect occasional re-approval noise.
- If wired into CI, make it a **hard-failing** step — a soft-failing test step gives false confidence.

## PR Review Checklist (public-surface changes)

- [ ] No removed/renamed public members (use `[Obsolete]` instead)
- [ ] No changed signatures (add an overload instead)
- [ ] No new required parameters; no optional params added to shipped methods
- [ ] No tightened inputs or changed serialised/wire output without a plan
- [ ] Wire-format changes are read-side-first / opt-in
- [ ] Breaking changes are intentional, versioned (major), and documented

## Anti-Patterns

- **Breaking changes disguised as fixes** — e.g. making a method `async` in place. Add a new method, deprecate the old.
- **Silent default/behaviour changes** — flipping a default value breaks callers who relied on the old one. Introduce a new opt-in instead.
- **Polymorphic serialisation with embedded type names** — renaming a class becomes a wire break.

## References

- [Semantic Versioning](https://semver.org/)
- [PublicApiGenerator](https://github.com/PublicApiGenerator/PublicApiGenerator)
