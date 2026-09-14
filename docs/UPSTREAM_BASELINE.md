# Upstream baseline

- Product: [OpenUsage v0.7.11](https://github.com/robinebers/openusage/releases/tag/v0.7.11)
- Verified commit: `753e2fe4bc82a011567d16d8deb88176b58ed24e`
- Vendor checkout: `vendor/openusage` (read-only reference, excluded from published packages)
- License: MIT, Copyright (c) 2026 Robin Ebers (see `/LICENSE`)
- Trademark: upstream brand is **not** MIT. This port uses the independent name **WinLLMUsage**.

WinLLMUsage is a C# / .NET 10 translation of the tagged Swift 6.2 menu-bar app. Provider IDs, resource keys, refresh cadence (5 minutes), CLI/HTTP contracts (`openusage.limits.v1`), and default layout IDs are taken from that tag.

This workspace was implemented on macOS. WPF tray compilation and live Windows credential decoding are **not** Windows-verified here.
