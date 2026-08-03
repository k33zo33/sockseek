# ADR-0004: Use Avalonia for the desktop UI

## Status
Accepted

## Context
The product needs a modern cross-platform .NET desktop UI while keeping the existing .NET engine and API stack.

## Decision
Use Avalonia with MVVM for the desktop application. Windows is the first release target, Linux the second, and macOS follows after stabilization.

## Consequences
- UI project must not reference `Sockseek.Core` or EF DbContext.
- UI state comes from immutable API snapshots and SignalR events.
- Headless ViewModel/navigation tests are required.
- Native player and OS media-session behavior require platform capability tests.
