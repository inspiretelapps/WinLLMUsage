# Windows verification status

Reviewed revision: `ab983a4`, 14 September 2026.

**Partial implementation; Windows solution build fails.** This is not solely a backlog of tests against a finished product. See the [implementation review](IMPLEMENTATION_REPORT.md).

## Observed evidence

- [Windows CI run 34876791647](https://github.com/inspiretelapps/WinLLMUsage/actions/runs/34876791647): failed build, three CS0246 errors for WPF `Window`/`RoutedEventArgs`. Tests did not run in that job.
- Re-run on macOS ARM64/.NET 10.0.105: 28 tests pass, covering selected core, mapper, reader, redaction and marker-file cases.
- No native desktop, clean-machine installer, DPAPI or live-provider acceptance run is recorded.
- `verify-install.ps1` is a placeholder that exits zero; it is not evidence of verification.

## Implementation prerequisites

1. Configure and connect the actual WPF target, XAML, STA application lifecycle and tray.
2. Correct provider protocols, rotated credentials, account/cache isolation and pricing/scanners (review R2–R5, R7–R8).
3. Finish settings/customization, hotkey, startup registration, notifications, privacy, pins and exports.
4. Implement folder sync and the confirmed reset-credit flow with mocked tests.
5. Implement Velopack packaging, CLI launcher and real install/upgrade/uninstall assertions.

## Native acceptance after prerequisites

- Clean Windows 11 x64 install without developer runtimes; launch, second-instance activation and exit.
- Tray overflow/Explorer restart, multiple monitors, 100/125/150/200% DPI and high contrast.
- Keyboard navigation, global-shortcut conflicts, launch-at-login on/off, notifications and privacy.
- Explicitly documented Windows credential formats/companion versions and opt-in live-provider smoke tests.
- Offline/relaunch/resume, account switching, concurrent GUI/CLI cache and credential access.
- Occupied API port, loopback-only reachability and contract/network behavior.
- Two-device folder sync including malformed data, account matching and disable/delete.
- Installed/portable CLI, upgrades, failed updates and uninstall preserving provider data.

Mark implementation defects fixed separately from tests passed. Do not advertise a complete port, ARM64 or Windows 10 support based on the macOS suite.
