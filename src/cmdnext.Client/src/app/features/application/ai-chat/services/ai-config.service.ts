import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';

export interface AiProvider {
  id: string;
  provider: string;
  endpoint?: string;
  keyLastFour?: string;
  isActive: boolean;
}

export interface AiSettings {
  id: string;
  defaultProvider?: string;
  defaultModel?: string;
  temperature: number;
  maxOutputTokens: number;
  isChatEnabled: boolean;
}

export interface AddProviderRequest {
  provider: string;
  apiKey: string;
  endpoint?: string;
}

export interface UpdateSettingsRequest {
  defaultProvider?: string;
  defaultModel?: string;
  temperature: number;
  maxOutputTokens: number;
  isChatEnabled: boolean;
}

@Injectable({ providedIn: 'root' })
export class AiConfigService {
  private readonly baseUrl = `${environment.apiUrl}/UserAiProvider`;

  constructor(private http: HttpClient) {}

  // Providers
  getProviders(): Observable<AiProvider[]> {
    return this.http.get<AiProvider[]>(`${this.baseUrl}/providers`);
  }

  addProvider(request: AddProviderRequest): Observable<AiProvider> {
    return this.http.post<AiProvider>(`${this.baseUrl}/providers`, request);
  }

  // Settings
  getSettings(): Observable<AiSettings> {
    return this.http.get<AiSettings>(`${this.baseUrl}/settings`);
  }

  updateSettings(request: UpdateSettingsRequest): Observable<AiSettings> {
    return this.http.put<AiSettings>(`${this.baseUrl}/settings`, request);
  }

  // Available providers for dropdown
  getAvailableProviders(): { name: string; displayName: string }[] {
    return [
      { name: 'mistral', displayName: 'Mistral AI' },
      { name: 'anthropic', displayName: 'Anthropic' }
    ];
  }

  // Available models per provider
  getAvailableModels(provider: string): { name: string; displayName: string }[] {
    const models: Record<string, { name: string; displayName: string }[]> = {
      mistral: [
        { name: 'mistral-tiny', displayName: 'Mistral Tiny' },
        { name: 'mistral-small', displayName: 'Mistral Small' },
        { name: 'mistral-medium', displayName: 'Mistral Medium' },
        { name: 'mistral-large', displayName: 'Mistral Large' },
        { name: 'mistral-large-latest', displayName: 'Mistral Large (Latest)' }
      ],
      anthropic: [
        { name: 'claude-opus-5', displayName: 'Claude Opus 5' },
        { name: 'claude-sonnet-5', displayName: 'Claude Sonnet 5' },
        { name: 'claude-haiku-4-5', displayName: 'Claude Haiku 4.5' }
      ]
    };
    return models[provider] || [];
  }
}
