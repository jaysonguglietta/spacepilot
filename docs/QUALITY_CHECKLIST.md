# Product Quality Checklist

Use this checklist before calling a SpacePilot change complete.

## Product

- The change supports disk cleanup, storage recovery, software maintenance, startup review, safety, or release readiness.
- The first visible experience remains the product workflow, not a marketing page.
- New workflows have a beginning, middle, and end.
- Empty, loading, success, warning, error, and disabled states are considered.
- Destructive or irreversible actions require confirmation.
- User-facing copy is specific to Windows cleanup and SpacePilot's safety model.

## Safety

- Cleanup candidates must come from approved rules or explicit user-selected personal-file workflows.
- Paths are checked against approved roots.
- Root directories are not deleted directly.
- Reparse points are skipped during cleanup scanning.
- Protected paths and protected extensions are honored.
- Quarantine remains the preferred default for file-changing cleanup.
- Registry cleaning, silent uninstalling, and silent startup disabling are not added.

## Interaction

- Visible controls work or are intentionally unavailable.
- Search, filter, sort, selection, and bulk actions update visible data when present.
- Forms validate input and explain how to recover from invalid values.
- Important actions produce status messages or activity log entries.
- Long-running operations set busy state and restore command availability afterward.

## Accessibility

- Controls have readable labels.
- Keyboard navigation works for core workflows.
- Focus states remain visible.
- Text contrast is sufficient.
- Error or warning messages are near the related action when possible.
- Text wraps without overlap in the main desktop window.

## Engineering

- Changes follow existing WPF, view-model, service, model, converter, and utility patterns.
- Windows-specific behavior stays inside services where practical.
- New data models are realistic and replaceable by future persistence/API layers.
- User input is validated.
- Errors are caught and surfaced with useful messages.
- No secrets or personal data samples are committed.

## Verification

- Run XML/XAML validation when markup changes.
- Run `node --check scripts/generate-brand-assets.mjs` when the asset generator changes.
- Run `dotnet build .\SpacePilot.sln -c Release` on Windows before release.
- Manually test affected workflows.
- Update docs when installation, usage, safety, privacy, or release behavior changes.
