# Personal AI Assistant

A self-hosted personal assistant I built for my own use and run daily — expenses, tasks, and
free-form notes, with an AI layer that can read and add to them through tool calling.

.NET 8 API, Angular 17 web client, Flutter iOS app, PostgreSQL + pgvector. Single-user by
construction, self-hosted.

> This is a personal project, not a product. I wrote it to replace a handful of apps I was
> paying for and to have somewhere to work with the AI APIs directly. It is on GitHub because
> the architecture is worth showing, not because it is looking for users.

## Why it exists

I wanted three things in one place: a receipt I can photograph and have logged, a task list
that does not nag me, and notes that are structured enough to search but loose enough to
actually write in. And I wanted to ask questions across all of it in plain language.

The interesting part turned out not to be the CRUD — it was deciding **what the model is
allowed to do** with my data, and building an AI layer that does not lock me to one vendor.

## What it does

**AI chat** — streaming replies over SSE, with file attachments. PDF, DOCX and XLSX are
converted to Markdown before they reach the model; images pass through as base64. Long
conversations are compacted rather than truncated. Token usage is tracked per request.

**Finance** — expenses down to line items, categories, and budgets. The part I use most:
photograph a supermarket receipt, and the model reads it and calls a tool to log every line
with quantity, unit price, and a canonical English name so `MILBONA REIS` and `BASMATI 1KG`
both group under `rice`. Then "how much did I spend on rice this month, and where was it
cheapest" actually works.

**Tasks** — a board with statuses and tags, plus a calendar view.

**Spaces** — the part I am happiest with. A space is a container with its own *entry schema*:
a "Learning" space starts with note and vocab entry types, a project space with tasks and
notes. The schema is copied onto the space at creation (`Space.SchemaJson`) so it can be edited
per-space afterwards without code changes. Entries are chunked, embedded into pgvector, and
searchable both by full text (`pg_trgm`) and by vector similarity.

**Mobile** — a Flutter iOS app covering chat, finance, tasks and spaces, with a rich-text note
editor. Built because photographing a receipt from a laptop is absurd.

## Design decisions

These are the choices I would want to talk about, rather than the feature list.

### The AI can read and add. It cannot change or delete.

`FinanceAiTools`, `DTaskAiTools` and `SpacesAiTools` expose **read and add only**. There is
deliberately no update, delete, or merge tool. A model mistake or a prompt injection in an
uploaded receipt cannot destroy records that already exist — the worst case is a spurious entry
I delete myself. Destructive actions are reachable only from the UI.

This cost me some convenience (I cannot ask it to fix a typo in an expense) and I would make
the same trade again.

### Provider-agnostic, because I did not want to bet on one vendor

`cmdnext.AI.Service` knows nothing about a specific provider. Each one implements
`IProviderClientBuilder` — currently Anthropic and Mistral — and is resolved through
`ChatClientFactory`. Embeddings follow the same shape via `IEmbeddingProviderBuilder`. Adding a
provider is a new builder class; the chat service does not change. It is built on
`Microsoft.Extensions.AI`, so tool calling and streaming are uniform across providers.

### My API keys, encrypted, per user

Provider credentials are entered in the app, stored AES-encrypted at rest, and resolved per
request by `AiCredentialResolver`. **No provider key is read from configuration or committed.**
The repo ships `.env.example` with empty values and a one-liner for generating them; the
compose file uses `${JWT_SECRET:?...}` so the stack refuses to start rather than falling back
to a placeholder secret.

### Single-user by construction

Every entity carries a `UserId` and every query filters on it. There is no tenant or
organisation concept to get wrong, because there is exactly one user: me.

### Retrieval over my own notes, not model memory

Entry bodies are chunked (`EntryChunk`) and embedded with `mistral-embed` into pgvector, so
chat answers are grounded in what I actually wrote.

## Architecture

```
src/
  cmdnext.Api/          ASP.NET Core 8 — controllers, SSE streaming, JWT auth
  cmdnext.AI.Service/   provider-agnostic AI layer (chat, embeddings, provider builders)
  cmdnext.Service/      business logic + the AI tool definitions
  cmdnext.Repository/   EF Core 8, unit of work over PostgreSQL
  cmdnext.Models/       entities and DTOs
  cmdnext.Migration/    EF migrations, applied at container startup
  cmdnext.Client/       Angular 17, standalone components
  cmdnext.Mobile/       Flutter iOS app
```

**Stack:** .NET 8 · ASP.NET Core · EF Core 8 · PostgreSQL 17 + pgvector + pg_trgm ·
Angular 17 · TypeScript 5.3 · Flutter/Dart · Serilog · Docker Compose · Microsoft.Extensions.AI

## Running it

Requires .NET 8 SDK, Node 18+, and PostgreSQL 17 with the `vector` and `pg_trgm` extensions.

```bash
cd src/cmdnext.Api    && dotnet run          # API on :5000
cd src/cmdnext.Client && npm i && ng serve   # web on :4200
```

Set the connection string and `JwtSettings:SecretKey` / `CryptoSettings:Key` through
environment variables or user-secrets — the committed `appsettings.json` has placeholders
only. EF migrations are applied on API startup, before Kestrel binds.

AI features need a provider key, added from the app's settings screen rather than config.

## Status and what is missing

Working and in daily use. Known gaps, honestly:

- **Test coverage is thin.** Plenty of manual verification, not much automated. The decimal
  handling in finance and the crypto helper are what I would cover first.
- Single-user only, by design. Multi-user would mean revisiting every query.
- Task reminders and recurring expenses are not built.
- The web client is functional rather than designed.
