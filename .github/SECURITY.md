# Security Policy

## Supported Versions

JellyFlare is actively maintained and tested against the latest stable release of Jellyfin.

## Reporting a Vulnerability

Please **do not open a public issue** for security vulnerabilities.

Report privately via GitHub: **Security → Report a vulnerability** on the repository page.
Include a description of the issue, reproduction steps, and your assessment of impact.

---

## Known Security Considerations

This document discloses the known security posture of JellyFlare. It is intended for users
who want to understand what they are running before installing the plugin.

### Architectural Trade-offs (By Design)

The following are not bugs. They are direct consequences of how the plugin must work.
They are documented here for transparency.

#### Trust chain through two third-party plugins

JellyFlare does not inject JavaScript directly. It relies on:

- **JS Injector** (n00bcodr): injects a loader into Jellyfin's served HTML
- **File Transformation** (IAmParadox27): performs the in-memory HTML rewrite

If either upstream plugin is compromised, all scripts they manage could be affected.
JellyFlare has no control over this trust chain. Audit upstream plugins independently
and pin to known-good versions.

#### Banner script runs with full page JavaScript privileges

Once injected, `banner.js` executes in the same JavaScript context as the rest of the
Jellyfin web app. This is true of all JS Injector scripts and of browser extensions —
it is the nature of client-side script injection. A compromised `banner.js` would have
full access to the page including any in-memory tokens.

The integrity of the banner depends on:

1. The integrity of this repository and its release process
2. The security of the GitHub account that publishes releases
3. The integrity of JS Injector and File Transformation

#### `history.pushState` / `replaceState` wrapping

To detect SPA navigation across all routing methods Jellyfin uses, the script wraps the
browser's native `history.pushState` and `history.replaceState` methods. This is
necessary because some Jellyfin page transitions (e.g. home → admin) go through
`pushState` rather than emitting a `hashchange` event. A debounce prevents race conditions
if multiple navigation events fire simultaneously.

#### Dismissed messages stored in `localStorage`

When the _Persist dismissed messages_ option is enabled, the text of dismissed messages
is stored in `localStorage` under the key `jf-dismissed-v1`. This data is limited to
message text the user already saw — no tokens, no personal data.

#### Cross-tab dismiss sync via `BroadcastChannel`

The script opens a `BroadcastChannel` named `"jf-banner-v1"` to propagate dismiss events
(single dismiss, dismiss all, permanent dismiss) to other tabs on the same origin. Messages
contain only a `type` string and, for single-dismiss events, the text of the dismissed
message — no tokens or personal data are transmitted. The channel is same-origin only;
cross-origin pages cannot post to it. If `BroadcastChannel` is unavailable the feature
is silently skipped.

---

### CI/CD Notes

Jellyfin does not enforce DLL signing for plugins. The MD5 checksum in `manifest.json`
provides download integrity (detects corruption or transit tampering) but is not a
cryptographic signature and does not prove authorship.

---

## Out of Scope

The following are not considered vulnerabilities in JellyFlare:

- Issues in Jellyfin itself, JS Injector, or File Transformation
- Attacks that require physical access to the server
- Attacks that require the attacker to already have Jellyfin admin credentials
- Social engineering of server administrators
