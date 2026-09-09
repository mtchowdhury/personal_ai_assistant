# cmdnext.Mobile — build plan

Flutter iOS app replacing the web client on mobile. iOS first, Android later
(the scaffold includes both platforms, but only iOS is verified).

## Stack — verified against installed versions, 2026-09-09

| Thing | Version | Notes |
|---|---|---|
| Flutter | 3.47.2 stable | Upgraded from 3.35.3 this session |
| Dart | 3.13.2 | |
| Xcode | 26.2 | iOS deployment target 15.0 |
| flutter_riverpod | 3.4.3 | **v3**: `Notifier`/`AsyncNotifier`, no `AutoDisposeNotifier`; `StateNotifierProvider` is legacy-only |
| go_router | 18.0.1 | Core API unchanged; `StatefulShellRoute` for the tab shell |
| dio | 5.11.1 | SSE needs `ResponseType.stream` |
| flutter_secure_storage | 11.0.0 | JWT lives here, not in prefs |
| intl | 0.20.3 | Dates and currency |
| image_picker | 1.2.3 | Receipt camera/gallery capture |

## Server

Reachable **only over the private network**, plain HTTP — there is no public hostname.

- hostname: `http://tawhid:8080` (survives a host address change)
- Raw IP: `http://localhost:8080`
- API base: `<host>/api/v1` — nginx proxies `/api` to the API container

Because it is plain HTTP, `Info.plist` needs an ATS exception (see step 2).
The server URL is editable in-app from the login screen and persisted, so a
rename or a local dev API does not require a rebuild.

## API facts that shape the client

- **Auth**: JWT bearer, `POST /auth/login` → `{ token, user, expiresIn }`.
  Response fields are **lowercase** here (`token`, not `Token`). No refresh
  token — a 401 means re-login.
- Everything else serializes **camelCase**.
- **Chat streams over SSE** (`POST /ai/sessions/{id}/messages/stream`).
  `EventSource` is unusable: it is GET-only and cannot send the bearer token,
  so the stream is read from the response body. Frames are `event: <name>` +
  `data: <json>`, separated by a blank line. Events: `message` (an
  `AiChatStreamUpdate`), `error` (`{code, message}`), `done`.
- **Spaces are schema-driven**: `SchemaJson` declares entry types and their
  fields, so entry forms are built at runtime, not compiled in.
- `Space.SchemaJson` and friends are JSON held in string columns — the API
  writes camelCase into them; parse, don't assume.
- **Finance dates *are* instants**, unlike task dates: `FinanceService` calls
  `.ToUniversalTime()` on `purchasedOn` and keeps the time of day, so local
  conversion is correct there. Check the service before assuming either way.
- **Dates are not instants.** `scheduledOn`/`dueOn` are calendar dates, but
  the column is `timestamp with time zone` and `DTaskService.NormalizeDate`
  stamps midnight as UTC, so they arrive as `...T00:00:00Z`. Converting them
  to local time shifts them across midnight — a task scheduled for today
  reads as yesterday from a negative UTC offset. `parseApiDate` takes the
  date parts verbatim; `parseApiInstant` is the one that converts.

## Steps

- [x] 0. Explore the API and web client; pin versions; upgrade Flutter
- [x] 1. Scaffold `src/cmdnext.Mobile`, add dependencies
- [x] 2. iOS config: bundle id, display name, ATS exception, deployment target
- [x] 3. Foundation: theme/design tokens, Dio client + auth interceptor,
      secure token store, editable server setting, error mapping
- [x] 4. Auth: login screen (with server field), session bootstrap, logout,
      auth-driven routing
- [x] 5. App shell: bottom tab navigation via `StatefulShellRoute`
- [x] 6. **Daily Tasks** — Today screen, quick add, task detail + inline edit,
      subtasks, swipe actions, all-tasks browse with filters, month calendar,
      and the board (one column at a time; move via a picker, not a drag)
- [x] 7. Finance — month dashboard with budget ring and category bars,
      expense list grouped by day, expense detail, budgets, receipt capture
- [ ] 8. AI Chat — SSE streaming, session list
- [ ] 9. Spaces — dynamic schema-driven entries, search
- [ ] 10. Dashboard/home — cross-feature summary
- [ ] 11. Polish: offline/empty/error states, pull-to-refresh, haptics, icon
- [ ] 12. Android pass (later)

## Design direction

The web layout is desktop-shaped (tables, side rails, dialogs) and does not
port directly. Mobile reinterprets rather than mirrors:

- Bottom tabs, not a sidebar.
- Lists of cards, not tables. Progressive disclosure over dense rows.
- Sheets for create/edit, not modal dialogs.
- One primary action per screen, thumb-reachable.
- Keep the web's blue accent `#2f6fed` and the task status colors so the two
  clients read as the same product.
