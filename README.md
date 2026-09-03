# CMDNEXT - Personal AI Assistant

## Overview

CMDNEXT is a **personal AI-powered assistant** application designed to help you manage your personal data including:

- **AI Chat** - Conversational AI assistant for any topic
- **Financial Records** - Track income, expenses, savings, and investments
- **Thoughts & Journal** - Write and organize personal reflections, ideas, and journal entries
- **Habit Tracking** - Build and track daily habits with progress monitoring

This project is a **scaffold** based on the the earlier scaffold AI chat architecture, but repurposed for personal use with different domain models and tools.

## Project Structure

The application follows a **layered architecture** with 7 main projects:

```
cmdnext/
├── cmdnext.sln                    # Visual Studio Solution
├── PROJECT_STRUCTURE.md           # Detailed project structure documentation
├── README.md                      # This file
│
├── src/
│   ├── cmdnext.Api/                # ASP.NET Core Web API (Backend)
│   │   ├── Controllers/           # REST API controllers
│   │   ├── Programs/              # Application entry point
│   │   ├── appsettings.json       # Configuration
│   │   └── cmdnext.Api.csproj
│   │
│   ├── cmdnext.AI.Service/        # AI Service Layer (Reusable)
│   │   └── Generic/
│   │       ├── Chat/              # Chat streaming services
│   │       ├── Configuration/     # AI settings
│   │       ├── Contracts/         # Interfaces & DTOs
│   │       ├── Embeddings/        # Vector embedding services
│   │       ├── Providers/         # AI provider builders (Mistral, Anthropic)
│   │       └── ConfigureGenericAiServices.cs
│   │
│   ├── cmdnext.Models/            # Domain Models & DTOs
│   │   └── Domain/
│   │       ├── Model/App/Ai/      # AI chat entities
│   │       ├── Model/App/Personal/ # Personal data entities
│   │       └── Model/App/Admin/   # User identity
│   │
│   ├── cmdnext.Repository/        # Data Access Layer
│   │   ├── Contracts/            # Repository interfaces
│   │   ├── Implementation/       # EF Core implementation
│   │   └── ConfigureServices.cs
│   │
│   ├── cmdnext.Service/           # Application Service Layer
│   │   ├── Services/             # Business logic services
│   │   ├── Contracts/            # Service interfaces
│   │   ├── Helpers/              # Utility classes
│   │   └── ConfigureServices.cs
│   │
│   └── cmdnext.Client/            # Angular 17 Frontend
│       ├── src/
│       │   ├── app/
│       │   │   ├── features/       # Feature modules
│       │   │   │   ├── application/ # Main app features
│       │   │   │   │   ├── ai-chat/  # AI chat component
│       │   │   │   │   ├── dashboard/
│       │   │   │   │   └── application.routes.ts
│       │   │   │   └── personal/    # Personal data features
│       │   │   │       ├── finance/
│       │   │   │       ├── thoughts/
│       │   │   │       └── habits/
│       │   │   ├── core/           # Core services & guards
│       │   │   │   ├── services/
│       │   │   │   └── guards/
│       │   │   └── shared/         # Shared components & pipes
│       │   ├── assets/
│       │   ├── environments/
│       │   ├── styles.scss        # Global styles
│       │   ├── index.html
│       │   ├── main.ts
│       │   └── app.config.ts
│       ├── package.json
│       ├── angular.json
│       └── tsconfig.json
│
└── tests/                              # Unit & Integration Tests
```

## Technology Stack

### Frontend
- **Angular 17** with Standalone Components
- **TypeScript 5.3**
- **SCSS** for styling
- **RxJS** for reactive programming
- **marked/ngx-markdown** for markdown rendering

### Backend
- **.NET 8** with ASP.NET Core
- **Entity Framework Core** for data access
- **Microsoft.Extensions.AI** for AI integration
- **OpenAI SDK** for Mistral compatibility
- **Anthropic SDK** for Anthropic support

### Database
- **SQL Server** (configurable)
- EF Core Migrations for schema management

### AI Providers
- **Mistral** (via OpenAI-compatible endpoint)
- **Anthropic** (native SDK)
- **Extensible** to add OpenAI, Gemini, etc.

## Key Features

### 1. AI Chat
- Streaming responses via Server-Sent Events (SSE)
- File attachments (Excel, Word, PDF, Images)
- Session management (create, rename, delete, list)
- Conversation compaction for long sessions
- Multiple AI profiles with custom prompts
- Token usage tracking

### 2. Financial Management
- Track income, expenses, savings, investments
- Categorize transactions
- Add tags and descriptions
- Attach receipts/documents
- Recurring transaction support
- AI-powered insights

### 3. Thought Journal
- Write and organize thoughts
- Tag entries for categorization
- Mood tracking
- Privacy settings
- AI analysis and summarization

### 4. Habit Tracking
- Create custom habits
- Track completion history
- Set reminders
- Progress visualization
- Streak tracking

## Configuration

### Backend Configuration (appsettings.json)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=CmdNext;..."
  },
  "JwtSettings": {
    "Issuer": "CmdNextAPI",
    "Audience": "CmdNextClient",
    "SecretKey": "YourSecretKeyHere",
    "ExpiryInMinutes": 240
  },
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
  "CryptoSettings": {
    "Key": "EncryptionKeyForAPIKeys"
  }
}
```

### Frontend Configuration (environment.ts)

```typescript
export const environment = {
  production: false,
  apiUrl: 'https://localhost:7230/api/v1.0'
};
```

## Getting Started

### Prerequisites

1. **.NET 8 SDK** - https://dotnet.microsoft.com/download
2. **Node.js 18+** - https://nodejs.org
3. **Angular CLI 17+** - `npm install -g @angular/cli`
4. **SQL Server** (or modify connection string for other databases)

### Setup

#### 1. Clone & Prepare
```bash
cd /Users/MySpace/Repos/cmdnext
```

#### 2. Backend Setup
```bash
# Navigate to API project
cd src/cmdnext.Api

# Install NuGet packages
dotnet restore

# Update appsettings.json with your configuration
# - Database connection string
# - JWT settings
# - AI provider API keys
# - Crypto key

# Create and apply database migrations
cd ../cmdnext.Migration
# TODO: Create migrations
# dotnet ef migrations add InitialCreate
# dotnet ef database update
```

#### 3. Frontend Setup
```bash
cd src/cmdnext.Client

# Install npm packages
npm install

# Start development server
ng serve
```

#### 4. Run Backend
```bash
cd src/cmdnext.Api
dotnet run
```

#### 5. Access Application
- Frontend: http://localhost:4200
- Backend API: https://localhost:7230 (or http://localhost:5000)
- Swagger UI: https://localhost:7230/swagger

## Architecture Highlights

### Key Differences from the earlier scaffold

| Feature | the earlier scaffold | CMDNEXT |
|---------|------|---------|
| **User Context** | Multi-tenant (Company) | Personal (User) |
| **Data Models** | Business entities | Personal data entities |
| **AI Profiles** | Support profile | Multiple specialized profiles |
| **Authentication** | Company-based | User-based (JWT) |
| **Data Storage** | Company-isolated | User-isolated |

### Database Schema

The application uses **3 separate schemas**:

1. **`ai`** - AI chat data
   - `AiChatSession` - Chat sessions
   - `AiChatMessage` - Chat messages
   - `AiUsageLog` - Token usage tracking
   - `UserAiSettings` - User AI configuration
   - `UserAiProvider` - User AI provider credentials

2. **`personal`** - Personal data
   - `FinancialRecord` - Financial transactions
   - `ThoughtEntry` - Journal/thought entries
   - `Habit` - Habit definitions
   - `HabitCompletion` - Habit completion history
   - `UserSettings` - User preferences

3. **`identity`** - Authentication
   - `User` - User accounts

## File Count

- **Total Files Created**: ~89 source files
- **Backend**: ~50 files (C#)
- **Frontend**: ~39 files (TypeScript, HTML, SCSS)

## What's Included

### Backend (C#)
- ✅ Solution file (cmdnext.sln)
- ✅ API project with controllers
- ✅ AI Service layer (Mistral, Anthropic support)
- ✅ Application service layer
- ✅ Repository layer with UnitOfWork pattern
- ✅ Domain models and DTOs
- ✅ Configuration and dependency injection

### Frontend (Angular)
- ✅ Angular 17 with standalone components
- ✅ AI Chat component (full implementation)
- ✅ Dashboard component
- ✅ Authentication (login, register)
- ✅ Routing configuration
- ✅ Services (AI chat, auth, notification)
- ✅ Guards (auth guard)
- ✅ Pipes (markdown)
- ✅ Global styles and responsive design

## Next Steps

To complete the application, you need to:

### 1. **Implement Remaining Services**
- [ ] `FinancialService.cs` - CRUD for financial records
- [ ] `ThoughtService.cs` - CRUD for thought entries
- [ ] `HabitService.cs` - CRUD for habits
- [ ] `UserService.cs` - User management

### 2. **Create Auth Controller**
```csharp
// cmdnext.Api/Controllers/AuthController.cs
[ApiController]
[Route("api/v1.0/[controller]")]
public class AuthController : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        // Implement user registration
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // Implement user login
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        // Return current user info
    }
}
```

### 3. **Implement Database Migrations**
```bash
cd src/cmdnext.Migration
dotnet ef migrations add InitialCreate
```

### 4. **Create Personal Feature Components**
- [ ] Finance list/view/add/edit components
- [ ] Thoughts list/view/add/edit components
- [ ] Habits list/view/add/edit components

### 5. **Add Personal Tools to AI**
Create AI tools that can:
- Analyze financial data
- Process thoughts for insights
- Generate habit recommendations

### 6. **Enhance UI**
- [ ] Add a proper notification system
- [ ] Add loading states
- [ ] Add error handling
- [ ] Add form validation
- [ ] Add charts for financial data

### 7. **Add Tests**
- [ ] Unit tests for services
- [ ] Integration tests for API
- [ ] Component tests for frontend

## Customization

### Adding a New AI Provider

1. Create a new provider builder:
```csharp
// cmdnext.AI.Service/Generic/Providers/OpenAiClientBuilder.cs
public sealed class OpenAiClientBuilder : IProviderClientBuilder
{
    public string Provider => AiProviders.OpenAI;

    public IChatClient Build(AiProviderCredential credential, ProviderOptions? options)
    {
        var client = new OpenAIClient(
            new ApiKeyCredential(credential.ApiKey),
            new OpenAIClientOptions { Endpoint = new Uri(options?.Endpoint ?? "https://api.openai.com/v1") });
        
        return client.GetChatClient(credential.Model).AsIChatClient();
    }
}
```

2. Register in ConfigureGenericAiServices.cs:
```csharp
services.AddSingleton<IProviderClientBuilder, OpenAiClientBuilder>();
```

3. Add to appsettings.json:
```json
"Providers": {
  "openai": { "IsEnabled": true, "Endpoint": "https://api.openai.com/v1" }
}
```

## License

This is a proprietary project. All rights reserved.

## Contributing

This is a personal project. Contributions are not currently accepted.

## Support

For issues or questions, please refer to the project documentation or create an issue in the repository.

---

**Project Status**: Scaffold Complete (Core architecture in place)
**Estimated Completion**: 40% (Core AI chat working, personal features to be implemented)
