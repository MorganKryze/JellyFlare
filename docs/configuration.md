# Configuration

Navigate to **Dashboard → Plugins → JellyFlare**. The page has four tabs.

## Permanent tab

A library of permanent banners that take priority over all rotation messages.
Select the active entry with its radio button; use the enable toggle to pause without losing any entries.

| Field             | Description                                                     |
| ----------------- | --------------------------------------------------------------- |
| Enable            | Toggle the permanent banner on/off (all entries)                |
| Radio button      | Select which entry is currently active                          |
| Text              | Message to display                                              |
| URL               | Optional link; clicking the banner opens this URL in a new tab  |
| Background colour | CSS colour value, e.g. `#2e7d32`                                |
| Text colour       | CSS colour value, e.g. `#fff`                                   |
| Schedule          | When to show this entry; see [schedule types](#schedules) below |

Each row is collapsed by default; click the row body (not the radio) to expand and edit.
Rows with empty text are ignored on save.

Entries can be reordered by dragging the ⠿ grip on the left of each row, useful for keeping the most-used entries at the top of the library.

## Rotation tab

| Field             | Description                                                           |
| ----------------- | --------------------------------------------------------------------- |
| Enable            | Toggle all rotation banners on/off                                    |
| Shuffle           | Show messages in random order (on by default); uncheck for sequential |
| Text              | Message to display                                                    |
| URL               | Optional link; clicking the banner opens this URL in a new tab        |
| Background colour | CSS colour value, e.g. `#1976d2`                                      |
| Text colour       | CSS colour value, e.g. `#fff`                                         |
| Schedule          | When to show this message; see [schedule types](#schedules) below     |

Each message row has its own enable checkbox; uncheck to pause a single message without removing it.
Messages that are disabled or outside their schedule are silently skipped.

Rows can be reordered by dragging the ⠿ grip on the left of each row. Order matters when **Shuffle** is off; messages play top-to-bottom in list order.

## Schedules

Each message and permanent entry has a **Schedule** selector with five options:

| Type   | When it shows                                       | Fields                                     |
| ------ | --------------------------------------------------- | ------------------------------------------ |
| Always | Always visible (default)                            | (none)                                     |
| Fixed  | Between two specific datetimes                      | Start, End (both optional)                 |
| Annual | Same calendar span every year (e.g. Dec 20 – Jan 5) | From Mo/Dd, To Mo/Dd; optional time window |
| Weekly | On specific days of the week                        | Day toggles (Su–Sa); optional time window  |
| Daily  | Every day within a time window                      | Time start, Time end                       |

Annual spans that cross year-end (e.g. December → January) are supported automatically.
The **Annual** panel includes one-click shortcuts for common holidays (Christmas, New Year's, Thanksgiving, Halloween, Valentine's, Summer, Easter).

## Settings tab

### Visibility

| Field                          | Default | Description                                                                                                                                     |
| ------------------------------ | ------- | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| Show banner in admin dashboard | off     | When enabled, the banner also appears on admin pages (dashboard, plugins, settings…). Disabled by default as the banner overlaps admin content. |

### Appearance

| Field              | Default | Description                                           |
| ------------------ | ------- | ----------------------------------------------------- |
| Text alignment     | Center  | Align banner text left or center                      |
| Font size (px)     | 14      | Base font size; mobile uses 1px smaller automatically |
| Banner height (px) | 36      | Height of the banner bar (24–80 px)                   |
| Bold text          | on      | Whether banner text is rendered bold                  |
| Transition speed   | Normal  | Fade speed: None, Fast, Normal, Slow                  |

### Timing

| Field                | Default | Description                                   |
| -------------------- | ------- | --------------------------------------------- |
| Display duration (s) | 30      | How long each message is shown before cycling |
| Pause duration (s)   | 60      | Gap between messages (0 = no pause)           |

### Controls

**Dismiss button size (px)** (default 20): font size of the × button; applies to both the permanent and rotation dismiss buttons.

**Permanent banner**

| Field               | Default | Description                                                                   |
| ------------------- | ------- | ----------------------------------------------------------------------------- |
| Show dismiss button | off     | Adds a × button to the permanent banner so users can close it for the session |

When _Show dismiss button_ is on and _Persist dismissed messages_ (Behaviour) is also on, the dismissal survives page reloads.

**Rotation messages**

| Field                       | Default    | Description                                 |
| --------------------------- | ---------- | ------------------------------------------- |
| Show dismiss button         | on         | Whether the per-message × button is visible |
| Show "hide all" button      | on         | Whether the "hide all" button is visible    |
| "Hide all" button size (px) | 10         | Font size of the "hide all" button          |
| "Hide all" button label     | `hide all` | Custom label for the "hide all" button      |

**Link popup**

When a banner has a URL, clicking it opens a small confirmation popup with **Open link**, **Copy URL**, and **Cancel** — instead of navigating directly, which avoids in-app WebViews hijacking the link.

| Field     | Default | Description                                                                                                     |
| --------- | ------- | --------------------------------------------------------------------------------------------------------------- |
| Hint text | _empty_ | Optional message shown above the buttons in the popup (e.g. "On mobile, copy the URL instead"). Empty = hidden. |

Inside the Jellyfin mobile app (Android / iOS), **Copy URL** is promoted to the blue primary style and **Open link** is de-emphasised, since opening external links from the in-app WebView is unreliable.

Each subsection heading has a small restore icon that resets only that subsection to defaults.

### Presets

A list of named colour presets available in all message editors. Each preset has a label, background colour, and text colour. Presets can be added, edited, reordered, and deleted. Deleting a preset keeps existing message colours but removes the selection indicator on affected messages.

The restore icon next to **Presets** resets the list to the 8 built-in defaults.

### Danger Zone

| Button               | Scope                                                                       |
| -------------------- | --------------------------------------------------------------------------- |
| Reset settings       | Restores Timing, Controls, and Presets to defaults; messages unchanged      |
| Wipe all plugin data | Clears the permanent banner, all rotation messages, and resets all settings |

Both buttons require confirmation before applying. Changes only take effect after clicking **Save**.

## Maintenance tab _(new)_

Put the server into maintenance mode: all non-admin users are disabled at the Jellyfin API
level and see a full-screen overlay on every page (including the login screen).

| Field           | Description                                                                                               |
| --------------- | --------------------------------------------------------------------------------------------------------- |
| Active          | Check to activate; uncheck to deactivate. Applied when you click **Save**.                                |
| Overlay message | Text displayed in the maintenance overlay (default: "Server under maintenance. Please check back later.") |
| Status page URL | Optional URL shown as a link in the overlay so users can check server status (http/https only)            |

**Activating** disables all non-admin, non-pre-disabled users at the API level.
**Deactivating** re-enables only the users that were enabled before maintenance was activated.
Users that were already disabled before activation are left unchanged.

Admins see a "Dismiss (admin)" button in the overlay so they can continue working without
deactivating maintenance. Non-admin users and unauthenticated visitors (login page) see the
overlay without a dismiss option.

### Automation

Schedule maintenance windows and server restarts without manual intervention. All times use your browser's local timezone and take effect within one minute.

**Scheduled maintenance**

| Field         | Description                                                                                                                                                 |
| ------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Activate at   | Date and time at which maintenance mode turns on automatically                                                                                              |
| Deactivate at | Optional. Date and time at which maintenance mode turns off. Must be after "Activate at". The schedule clears automatically once the full window completes. |

**Scheduled restart**

| Field      | Description                                                                                    |
| ---------- | ---------------------------------------------------------------------------------------------- |
| Restart at | Optional. Date and time at which the server restarts once. The field clears after the restart. |

> Changes take effect as soon as you click **Save**; no page reload required for connected clients.
