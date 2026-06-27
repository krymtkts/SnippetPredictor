# Changelog

This file records all notable changes to this project.

This changelog uses the [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) format.
This project uses prerelease versions such as 0.1.0-alpha.

## [Unreleased]

## [0.6.0] - 2026-06-27

### Fixed

- Refine the regular expression pattern to support leading whitespace in snippet input.

## [0.5.0] - 2025-12-30

### Added

- Add a debounce mechanism for snippet refresh to optimize file change handling.

### Changed

- Update help documentation.

### Fixed

- Fix a potential group serialization error by returning an empty string instead of null.

## [0.4.0] - 2025-05-10

### Added

- Add group ID suggestions for empty input in snippet search.

### Changed

- Add predictor lifecycle management to manage resources more reliably.
- Refine cmdlet documentation.
- Add project metadata for description and copyright information.

### Fixed

- Fix `Get-Snippet` to return a single `SnippetEntry` instead of an array.

## [0.3.0] - 2025-04-12

### Added

- Add `SearchCaseSensitive` option to `SnippetConfig` for configurable case sensitivity.

### Changed

- Update documentation to describe case-sensitive search configuration.

## [0.2.0] - 2025-03-22

### Added

- Add `Group` property to `SnippetEntry` for organizing snippets and supporting group filtering.
- Add tooltip-based suggestion support, including formatting the group in tooltips when defined.

### Changed

- Change default snippet search to support case-insensitive matching.
- Update module description to specify PowerShell and PSReadLine version requirements.

### Fixed

- Support single quotes in snippet JSON examples by using a custom JSON encoder.

## [0.1.0] - 2025-02-23

### Added

- Add help documentation for the `SnippetPredictor` module and cmdlets.

### Changed

- Adjust JSON serialization options to allow case-insensitive field names in snippet configuration.
- Rename the `Text` parameter to `Snippet` in basic cmdlets for improved usability.
- Retrieve an empty array instead of throwing an error when snippet configuration is not found.

## [0.1.0-beta] - 2025-02-21

### Added

- Add basic cmdlets to manage snippets: `Get-Snippet`, `Add-Snippet`, and `Remove-Snippet`.
- Add automatic snippet refreshing on configuration file changes using a file watcher.
- Add a snippet symbol to exclude other predictors from suggestions.

### Fixed

- Trim input after removing the snippet symbol.

## [0.1.0-alpha] - 2025-02-11

### Added

- Add initial snippet loading and filtering functionality.
- Support PowerShell custom predictor interface (`ISubsystemPredictor`).

### Notes

- This is the initial alpha release of `SnippetPredictor`.
- Supported PowerShell versions are 7.2 and higher.

---

[Unreleased]: https://github.com/krymtkts/SnippetPredictor/compare/v0.6.0...HEAD
[0.6.0]: https://github.com/krymtkts/SnippetPredictor/compare/v0.5.0...v0.6.0
[0.5.0]: https://github.com/krymtkts/SnippetPredictor/compare/v0.4.0...v0.5.0
[0.4.0]: https://github.com/krymtkts/SnippetPredictor/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/krymtkts/SnippetPredictor/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/krymtkts/SnippetPredictor/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/krymtkts/SnippetPredictor/compare/v0.1.0-beta...v0.1.0
[0.1.0-beta]: https://github.com/krymtkts/SnippetPredictor/compare/v0.1.0-alpha...v0.1.0-beta
[0.1.0-alpha]: https://github.com/krymtkts/SnippetPredictor/releases/tag/v0.1.0-alpha
