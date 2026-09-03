# CMDNEXT - Personal AI Assistant Project Structure

## Overview

CMDNEXT is a personal AI-powered assistant application for managing personal data including financial records, thoughts/journal entries, and habits. It follows the same architectural pattern as the the earlier scaffold AI chat feature but is designed for personal use rather than multi-tenant business use.

## Architecture

The application follows a **layered architecture** with clear separation of concerns:

```
┌─────────────────────────────────────────────────────────────┐
│                         FRONTEND                               │
│  Angular 17 Application (Standalone Components)               │
├─────────────────────────────────────────────────────────────┤
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │   Features    │  │   Core       │  │   Shared     │     │
│  │  - AI Chat   │  │  - Auth      │  │  - Components│     │
│  │  - Finance   │  │  - Guards    │  │  - Pipes     │     │
│  │  - Thoughts  │  │  - Services  │  │  - Directives │     │
│  │  - Habits   │  │  - Intercept.│  │  - Models    │     │
│  │  - Dashboard│  │              │  │              │     │
│  └──────────────┘  └──────────────┘  └──────────────┘     │
└─────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────┐
│                         BACKEND                                │
├─────────────────────────────────────────────────────────────┤
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │  API Layer   │  │ Service Layer │  │ AI Service   │     │
│  │  - Controllers│  │  - Business   │  │  - Chat      │     │
│  │  - DTOs      │  │    Logic     │  │  - Embeddings│     │
│  │  - Middleware│  │  - Validation │  │  - Providers │     │
│  └──────────────┘  └──────────────┘  └──────────────┘     │
│                                                                  │
│  ┌──────────────┐  ┌──────────────┐                                  │
│  │ Models/DTOs  │  │ Repository   │                                  │
│  │  - Entities  │  │  - UnitOfWork │                                  │
│  │  - DTOs      │  │  - Generic Rep│                                  │
│  │  - Enums     │  │  - EF Core    │                                  │
│  └──────────────┘  └──────────────┘                                  │
└─────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────┐
│                      DATA STORAGE                               │
│  ┌──────────────┐  ┌──────────────┐                              │
│  │ SQL Server   │  │  - ai schema │                              │
│  │  - Sessions  │  │  - personal   │                              │
│  │  - Messages  │  │    schema    │                              │
│  │  - Usage Logs│  │  - identity   │                              │
│  │  - Financial │  │    schema    │                              │
│  │  - Thoughts  │  │              │                              │
│  │  - Habits    │  │              │                              │
│  └──────────────┘  └──────────────┘                              │
└─────────────────────────────────────────────────────────────┘
```

## Project Structure

```
cmdnext/
├── cmdnext.sln                    # Visual Studio Solution
│
├── src/
│   ├── cmdnext.Api/                # ASP.NET Core Web API
│   │   ├── Controllers/            # API Controllers
│   │   │   ├── AiController.cs     # AI Chat endpoints
│   │   │   └── ... (other controllers)
│   │   ├── Programs/
│   │   │   └── Program.cs          # Application entry point
│   │   ├── appsettings.json        # Configuration
│   │   └── cmdnext.Api.csproj
│   │
│   ├── cmdnext.AI.Service/        # AI Service Layer
│   │   └── Generic/
│   │       ├── Chat/              # Chat services
│   │       │   └── AiChatService.cs
│   │       ├── Configuration/     # AI configuration
│   │       │   ├── AiOptions.cs
│   │       │   ├── AiProviders.cs
│   │       │   ├── ProfileOptions.cs
│   │       │   └── ProviderOptions.cs
│   │       ├── Contracts/         # Interfaces & DTOs
│   │       │   ├── AiChatRequest.cs
│   │       │   ├── AiChatResponse.cs
│   │       │   ├── AiChatStreamUpdate.cs
│   │       │   ├── AiNotConfiguredException.cs
│   │       │   ├── AiProviderCredential.cs
│   │       │   ├── AiUsage.cs
│   │       │   ├── AiSummarizeRequest.cs
│   │       │   ├── IChatClientFactory.cs
│   │       │   ├── IAiChatService.cs
│   │       │   └── IProviderClientBuilder.cs
│   │       ├── Embeddings/        # Embedding services
│   │       │   ├── EmbeddingClientFactory.cs
│   │       │   ├── IEmbeddingClientFactory.cs
│   │       │   ├── IEmbeddingProviderBuilder.cs
│   │       │   └── MistralEmbeddingClientBuilder.cs
│   │       ├── Providers/          # AI Provider builders
│   │       │   ├── AnthropicClientBuilder.cs
│   │       │   ├── ChatClientFactory.cs
│   │       │   └── MistralClientBuilder.cs
│   │       └── ConfigureGenericAiServices.cs
│   │
│   ├── cmdnext.Service/           # Application Service Layer
│   │   ├── Services/              # Business services
│   │   │   ├── AiConversationService.cs
│   │   │   ├── AiCredentialResolver.cs
│   │   │   ├── FinancialService.cs
│   │   │   ├── HabitService.cs
│   │   │   ├── ThoughtService.cs
│   │   │   └── UserService.cs
│   │   ├── Contracts/             # Service interfaces
│   │   │   ├── IAiConversationService.cs
│   │   │   ├── IAiCredentialResolver.cs
│   │   │   ├── ICryptoHelper.cs
│   │   │   └── ... (other contracts)
│   │   ├── Helpers/              # Helper classes
│   │   │   ├── AiAttachmentHelper.cs
│   │   │   └── AesCryptoHelper.cs
│   │   └── ConfigureServices.cs
│   │
│   ├── cmdnext.Models/            # Domain Models
│   │   └── Domain/
│   │       ├── DTOs/
│   │       │   ├── Ai/
│   │       │   │   └── AiChatDtos.cs
│   │       │   └── Constants/
│   │       │       └── DBSchema.cs
│   │       └── Model/
│   │           ├── Abstraction/
│   │           │   └── BaseEntity.cs
│   │           ├── App/
│   │           │   ├── Ai/
│   │           │   │   ├── AiChatMessage.cs
│   │           │   │   ├── AiChatSession.cs
│   │           │   │   ├── AiUsageLog.cs
│   │           │   │   ├── CompanyAiProvider.cs (renamed to UserAiProvider.cs)
│   │           │   │   └── CompanyAiSettings.cs (renamed to UserAiSettings.cs)
│   │           │   ├── Admin/
│   │           │   │   └── User.cs
│   │           │   └── Personal/
│   │           │       ├── FinancialRecord.cs
│   │           │       ├── Habit.cs
│   │           │       ├── ThoughtEntry.cs
│   │           │       └── UserSettings.cs
│   │
│   ├── cmdnext.Repository/         # Data Access Layer
│   │   ├── Contracts/
│   │   │   ├── IRepository.cs
│   │   │   └── IUnitOfWork.cs
│   │   ├── Implementation/
│   │   │   ├── CmdNextDbContext.cs
│   │   │   ├── Repository.cs
│   │   │   └── UnitOfWork.cs
│   │   └── ConfigureServices.cs
│   │
│   └── cmdnext.Client/             # Angular Frontend
│       ├── src/
│       │   ├── app/
│       │   │   ├── app.component.ts
│       │   │   ├── app.config.ts
│       │   │   ├── app.routes.ts
│       │   │   │
│       │   │   ├── core/
│       │   │   │   ├── guards/
│       │   │   │   │   └── auth.guard.ts
│       │   │   │   ├── interceptors/
│       │   │   │   │   └── auth.interceptor.ts
│       │   │   │   └── services/
│       │   │   │       ├── notification.service.ts
│       │   │   │       └── auth.service.ts
│       │   │   │
│       │   │   ├── features/
│       │   │   │   ├── application/
│       │   │   │   │   ├── application.routes.ts
│       │   │   │   │   ├── dashboard/
│       │   │   │   │   │   ├── dashboard.component.ts
│       │   │   │   │   │   └── dashboard.routes.ts
│       │   │   │   │   └── ai-chat/
│       │   │   │   │       ├── ai-chat.routes.ts
│       │   │   │   │       └── components/
│       │   │   │   │           └── ai-chat/
│       │   │   │   │               ├── ai-chat.component.ts
│       │   │   │   │               ├── ai-chat.component.html
│       │   │   │   │               ├── ai-chat.component.scss
│       │   │   │   │               └── services/
│       │   │   │   │                   └── ai-chat.service.ts
│       │   │   │   │
│       │   │   │   └── personal/
│       │   │   │       ├── finance/
│       │   │   │       │   ├── finance.routes.ts
│       │   │   │       │   ├── components/
│       │   │   │       │   └── services/
│       │   │   │       │
│       │   │   │       ├── thoughts/
│       │   │   │       │   ├── thoughts.routes.ts
│       │   │   │       │   ├── components/
│       │   │   │       │   └── services/
│       │   │   │       │
│       │   │   │       └── habits/
│       │   │   │           ├── habits.routes.ts
│       │   │   │           ├── components/
│       │   │   │           └── services/
│       │   │   │
│       │   │   └── shared/
│       │   │       ├── components/
│       │   │       │   └── chat/
│       │   │       │       └── chat.component.ts
│       │   │       ├── pipes/
│       │   │       │   └── markdown.pipe.ts
│       │   │       └── models/
│       │   │
│       │   ├── assets/
│       │   │   └── i18n/
│       │   │       ├── en.json
│       │   │       └── fr.json
│       │   │
│       │   ├── environments/
│       │   │   ├── environment.ts
│       │   │   └── environment.prod.ts
│       │   │
│       │   ├── index.html
│       │   ├── main.ts
│       │   └── styles.scss
│       │
│       ├── angular.json
│       ├── package.json
│       ├── tsconfig.json
│       ├── tsconfig.app.json
│       └── tsconfig.spec.json
│
└── tests/                               # Unit & Integration Tests
    └── ...

```

## Key Differences from the earlier scaffold

### 1. **User Context**
- **the earlier scaffold**: Multi-tenant (Company-based)
- **CMDNEXT**: Personal (Single user)
- All references to `CompanyId` replaced with `UserId`
- `CompanyAiSettings` → `UserAiSettings`
- `CompanyAiProvider` → `UserAiProvider`

### 2. **Data Models**
- **the earlier scaffold**: Business-focused entities
- **CMDNEXT**: Personal data entities
  - `FinancialRecord` - Track income, expenses, savings, investments
  - `ThoughtEntry` - Journal entries, ideas, reflections
  - `Habit` & `HabitCompletion` - Habit tracking
  - `UserSettings` - Personal preferences

### 3. **AI Profiles**
- **the earlier scaffold**: Support profile
- **CMDNEXT**: Multiple profiles
  - `PersonalAssistant` - General assistance
  - `FinancialAdvisor` - Financial guidance
  - `LifeCoach` - Personal development

### 4. **Data Storage**
- Uses SQL Server with separate schemas:
  - `ai` - AI chat sessions and messages
  - `personal` - Financial records, thoughts, habits
  - `identity` - Users

### 5. **Security**
- JWT-based authentication
- API keys encrypted at rest
- User isolation at database level

## Configuration

### Backend (appsettings.json)
```json
{
  "AI": {
    "MaxHistoryMessages": 40,
    "CompactionThreshold": 30,
    "CompactionKeepRecent": 10,
    "DefaultMaxOutputTokens": 4096,
    "DefaultTemperature": 0.7,
    "Providers": {
      "anthropic": { "IsEnabled": true },
      "mistral": { "IsEnabled": true, "Endpoint": "https://api.mistral.ai/v1" }
    },
    "Profiles": {
      "PersonalAssistant": { "SystemPrompt": "...", "Temperature": 0.3 },
      "FinancialAdvisor": { "SystemPrompt": "...", "Temperature": 0.2 },
      "LifeCoach": { "SystemPrompt": "...", "Temperature": 0.5 }
    }
  },
  "JwtSettings": { ... },
  "CryptoSettings": { "Key": "..." },
  "ConnectionStrings": { "DefaultConnection": "..." }
}
```

### Frontend (environment.ts)
```typescript
export const environment = {
  production: false,
  apiUrl: 'https://localhost:7230/api/v1.0'
};
```

## Technology Stack

### Frontend
- Angular 17 (Standalone Components)
- TypeScript
- SCSS
- RxJS
- Angular Material (optional)
- ngx-markdown (for rendering markdown)

### Backend
- .NET 8
- ASP.NET Core
- Entity Framework Core
- Microsoft.Extensions.AI
- OpenAI SDK (for Mistral)
- Anthropic SDK

### Database
- SQL Server
- EF Core for data access

### AI Providers
- Mistral (OpenAI-compatible endpoint)
- Anthropic
- Extensible to support OpenAI, Gemini

## Running the Application

### Backend
```bash
cd src/cmdnext.Api
dotnet run
```

### Frontend
```bash
cd src/cmdnext.Client
npm install
ng serve
```

## Next Steps

1. **Implement remaining services**:
   - `FinancialService`
   - `ThoughtService`
   - `HabitService`
   - `UserService`

2. **Create Angular components**:
   - Dashboard component
   - Finance management UI
   - Thought/journal UI
   - Habit tracking UI

3. **Add authentication**:
   - Implement AuthService
   - Login/Registration pages
   - JWT token management

4. **Database migrations**:
   - Create initial migration
   - Apply to database

5. **Configure AI providers**:
   - Add API keys to settings
   - Test connections

6. **Add personal tools**:
   - Financial analysis tools
   - Thought processing tools
   - Habit tracking tools

## File Count Summary

- **Backend Projects**: 6 (Api, AI.Service, Service, Models, Repository, Migration)
- **Frontend**: 1 (Angular)
- **Total**: ~100+ files (estimated)

## Notes

This scaffold provides the foundation. You'll need to:
1. Complete the service implementations
2. Create the Angular components
3. Add authentication
4. Configure your database
5. Add your AI provider credentials
6. Customize the AI profiles and system prompts for your use case
