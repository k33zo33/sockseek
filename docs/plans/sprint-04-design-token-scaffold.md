# Sprint 4 desktop design token scaffold

## Goal

Add a typed desktop design-token catalog and attach concrete token keys to the existing shell placeholders so the Sprint 4 desktop shell is ready for real Avalonia resource wiring.

## Why this slice

- Sprint 4 explicitly calls for desktop design tokens and localization-ready resources.
- The current shell foundation already exposes resource keys, but visual chrome still only carries plain strings.
- A tokenized shell model lets future Avalonia views bind to stable surface/icon/typography/spacing keys without reworking the current headless navigation models.

## In scope

- Add a typed `DesktopDesignTokens` catalog.
- Expose surface/icon/typography/spacing tokens from shell page, sidebar item, status banner, player bar, and command palette models.
- Cover the token assignments with focused desktop tests.

## Out of scope

- Actual Avalonia XAML resource dictionaries.
- Runtime theme switching visuals beyond existing theme preference state.
- Final icon assets or color values.

## Testing

- Extend existing desktop shell tests to verify tokenized state for default pages, banner states, sidebar items, and player bar placeholders.
