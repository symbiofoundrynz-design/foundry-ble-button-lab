# Project background and verification boundaries

## Origin

Foundry BLE Button Lab v0.1 was built as a standalone Windows desktop BLE diagnostics utility during investigation of inexpensive SXK/iSearching anti-loss/selfie-button tags for possible robotics use.

The original archived package was BANKED to the SymbioFoundry Library on **2026-10-01** as a known-good diagnostic baseline. The source and release artefacts in that private packet were verified at banking time. This repository candidate was prepared later by selecting project source, removing local device filters and writing new public documentation.

## Original diagnostic functionality

The historical release records report successful BLE advertisement scanning, accessible-device inspection, address-type-aware Windows device resolution and advertisement fingerprinting. The physical SXK/iSearching tags themselves consistently failed validation and were parked. No third-party legacy iSearching APK is included.

## Public-source changes

- Removed three hard-coded Bluetooth addresses and three household device-name filters from `BleController.KnownNoise`.
- `KnownNoise` now returns false; background discovery noise may increase relative to the archived executable.
- Omitted original compiled Windows release binaries, build outputs, local captures and archived source backups.
- Preserved the remainder of the current C#/WPF source files from the verified Library ZIP.

## Verification and caveats

- **Archived original binary:** previously validated as a BLE diagnostic baseline; the archived executable SHA-256 is documented privately in the Library intake record.
- **Published-source candidate:** archive integrity, file inventory and static sanitization checks performed; no Windows compile, executable equivalence or hardware retest completed for this edited version.
- Dependencies: project uses .NET 10, Windows BLE APIs and WPF. Target framework specifies Windows SDK compatibility `10.0.26100.0`. No third-party runtime binaries are bundled.
- The application can show device addresses, names and advertisement payloads in its own UI during operation; exercise care when capturing or sharing screenshots or logs.
- `Subscribe All` and pairing/connection features should be used only against devices you own or have permission to test.

## Future work (not implemented)

Configurable discovery exclusions, export controls, expanded advertisement decoding, historical candidate analysis and more deliberate GATT watch tooling are possible improvements, not release features.
