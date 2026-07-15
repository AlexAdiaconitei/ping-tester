# Changelog

This file keeps Ping Tester's permanent change history. Each version must remain here even after `RELEASE-NOTES.md` is replaced to prepare the next release.

Before publishing a version, move the entries under **Unreleased** to a section named using the format `## [1.2.3] - YYYY-MM-DD`, then leave the **Unreleased** section empty again.

## [Unreleased]

### Added

- Portable Windows 10 and Windows 11 desktop application distributed as a single executable.
- Configurable tests against multiple IP addresses or hostnames.
- Real-time latency, packet loss, outage, chart, and individual sample views.
- Local run history and CSV and JSON result exports.
- External CSV and JSON result viewing without modifying the original files.
- English and Spanish interfaces.
- Manual GitHub Actions workflow for building and publishing versioned releases.

### Changed

- Moved the maintained embedded PowerShell runner to `src/Resources/ping_test.ps1`; files under `.old/` are now legacy-only build exclusions.
- Standardized the release documentation as English `CHANGELOG.md` and `RELEASE-NOTES.md` files.
