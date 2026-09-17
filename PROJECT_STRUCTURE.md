# Project structure

Generated against the actual tree. If something is not here, it is not built.

## Solution layout

```
cmdnext.sln

src/
├── cmdnext.Api/                    ASP.NET Core 8
│   ├── Controllers/                Ai, Auth, DTasks, Finance, Spaces, UserAiProvider
│   ├── Infrastructure/             SSE writer, exception handling, startup wiring
│   └── appsettings.json            no secrets — see .env.example
│
├── cmdnext.AI.Service/             provider-agnostic AI layer
│   └── Generic/
│       ├── Chat/                   AiChatService — streaming, compaction, tool loop
│       ├── Providers/              AnthropicClientBuilder, MistralClientBuilder,
│       │                          ChatClientFactory
│       ├── Embeddings/             EmbeddingClientFactory, MistralEmbeddingClientBuilder
│       ├── Configuration/          AiOptions, ProviderOptions, ProfileOptions
│       └── Contracts/              interfaces + DTOs, AiNotConfiguredException
│
├── cmdnext.Service/                business logic
│   ├── Services/                   AiConversationService, AiCredentialResolver,
│   │                              FinanceService, DTaskService, SpaceService,
│   │                              EntryEmbeddingService, UserService
│   ├── Tools/                      AI-callable tools + per-request factories
│   │                              (Finance, DTask, Spaces)
│   ├── Spaces/                     SpaceTemplates — built-in entry schemas
│   └── Helpers/                    AesCryptoHelper, AiAttachmentHelper
│
├── cmdnext.Repository/             EF Core 8
│   ├── Implementation/             CmdNextDbContext, generic Repository, UnitOfWork
│   └── Contracts/                  IRepository, IUnitOfWork
│
├── cmdnext.Models/
│   └── Domain/
│       ├── Model/App/              Ai, Finance, DTasks, Spaces, Admin entities
│       ├── Model/Abstraction/      BaseEntity
│       └── DTOs/                   request/response contracts per feature
│
├── cmdnext.Migration/              EF migrations, applied at API startup
│
├── cmdnext.Client/                 Angular 17, standalone components
│   └── src/app/
│       ├── core/                   guards, interceptors, services, shared components
│       ├── features/auth/          login, register
│       └── features/application/
│           ├── ai-chat/            chat + provider config UI
│           ├── finance/            expenses, budgets, categories, canonical names
│           ├── dtasks/             board, calendar, list, status/tag managers
│           ├── spaces/             space list/detail, entry detail, search, settings
│           └── dashboard/
│
└── cmdnext.Mobile/                 Flutter iOS
    └── lib/
        ├── core/                   api client, auth, storage, theme, widgets
        └── features/               chat, finance, tasks, spaces, home
```

## Database schemas

Four Postgres schemas, all single-user (every table carries `UserId`):

| Schema | Tables |
|---|---|
| `identity` | `User` |
| `ai` | `AiChatSession`, `AiChatMessage`, `AiUsageLog`, `UserAiSettings`, `UserAiProvider` |
| `finance` | `Expense`, `ExpenseItem`, `Category`, `Budget` |
| `dtasks` | `DailyTask`, `TaskStatus`, `TaskTag` |
| `spaces` | `Space`, `Node`, `Entry`, `Attachment`, `EntryChunk` |

Extensions: `vector` (pgvector, embeddings) and `pg_trgm` (fuzzy full-text search).

## Request flow — AI chat with a tool call

1. `AiController` opens an SSE response.
2. `AiConversationService` loads session history, compacting it if over threshold.
3. `AiCredentialResolver` resolves the user's provider and decrypts their API key.
4. `ChatClientFactory` returns an `IChatClient` for that provider.
5. Tool factories bind the user's id into `FinanceAiTools` / `DTaskAiTools` / `SpacesAiTools`,
   so a tool can only ever touch that user's rows.
6. `AiChatService` streams deltas; tool calls are executed and fed back until the model stops.
7. Usage is written to `AiUsageLog`.

## Configuration

Secrets are never committed. The connection string, `JwtSettings:SecretKey` and
`CryptoSettings:Key` come from environment variables or user-secrets; the committed
`appsettings.json` holds placeholders only.

AI provider keys are not configuration at all — they are entered in the app and stored
AES-encrypted per user.

Non-secret AI settings that do live in `appsettings.json`:

```json
"AI": {
  "MaxHistoryMessages": 40,
  "CompactionThreshold": 30,
  "CompactionKeepRecent": 10,
  "DefaultMaxOutputTokens": 4096,
  "RequestTimeoutSeconds": 300,
  "Providers": {
    "anthropic": { "IsEnabled": true },
    "mistral":   { "IsEnabled": true, "Endpoint": "https://api.mistral.ai/v1" }
  },
  "Profiles": { "Assistant": { "SystemPrompt": "...", "Temperature": 0.7 } },
  "Embedding": { "Provider": "mistral", "Model": "mistral-embed", "Dimensions": 1024 }
}
```
