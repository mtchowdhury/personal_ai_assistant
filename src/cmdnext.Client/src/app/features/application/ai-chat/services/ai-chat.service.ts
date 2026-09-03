import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { AuthService } from '@core/services/auth.service';

export interface AiChatSession {
  id: string;
  title: string | null;
  lastMessageAt: string | null;
  createdOn: string | null;
  provider?: string | null;
  model?: string | null;
  spaceId?: string | null;
}

export interface AiChatMessage {
  id: string;
  role: string;
  content: string | null;
  sequence: number;
  isError: boolean;
  createdOn: string | null;
  attachments: string[];
  finishReason?: string | null;
  inputTokens?: number | null;
  outputTokens?: number | null;
}

export interface AiChatSessionDetail extends AiChatSession {
  messages: AiChatMessage[];
}

export interface AiChatAttachment {
  fileName: string;
  content: string;
}

export enum AiStreamPhase {
  Compacting = 0,
  Generating = 1,
  Completed = 2
}

export interface AiChatStreamUpdate {
  phase: AiStreamPhase;
  textDelta: string | null;
  isFinal: boolean;
  usage?: { inputTokens: number; outputTokens: number; totalTokens: number } | null;
  finishReason?: string | null;
}

export type AiStreamEvent =
  | { kind: 'update'; update: AiChatStreamUpdate }
  | { kind: 'error'; code: string; message: string }
  | { kind: 'done' };

@Injectable({ providedIn: 'root' })
export class AiChatService {
  private readonly baseUrl = `${environment.apiUrl}/ai`;

  constructor(private http: HttpClient, private auth: AuthService) {}

  getSessions(): Observable<AiChatSession[]> {
    return this.http.get<AiChatSession[]>(`${this.baseUrl}/sessions`);
  }

  getSession(sessionId: string): Observable<AiChatSessionDetail> {
    return this.http.get<AiChatSessionDetail>(`${this.baseUrl}/sessions/${sessionId}`);
  }

  createSession(title?: string, profileName?: string, spaceId?: string | null): Observable<AiChatSession> {
    return this.http.post<AiChatSession>(`${this.baseUrl}/sessions`, { title, profileName, spaceId });
  }

  renameSession(sessionId: string, title: string): Observable<unknown> {
    return this.http.put(`${this.baseUrl}/sessions/${sessionId}/title`, { title });
  }

  deleteSession(sessionId: string): Observable<unknown> {
    return this.http.delete(`${this.baseUrl}/sessions/${sessionId}`);
  }

  compactSession(sessionId: string): Observable<{ compacted: boolean }> {
    return this.http.post<{ compacted: boolean }>(`${this.baseUrl}/sessions/${sessionId}/compact`, {});
  }

  /**
   * Streams the assistant reply over SSE. EventSource cannot be used here because it
   * only issues GET requests and cannot attach the Authorization header, so the
   * stream is read manually from the fetch body.
   */
  async *streamMessage(
    sessionId: string,
    message: string,
    attachments?: AiChatAttachment[],
    signal?: AbortSignal
  ): AsyncGenerator<AiStreamEvent> {
    const token = this.auth.getToken();
    const headers: Record<string, string> = {
      'Content-Type': 'application/json'
    };
    // The interceptor cannot reach this manual fetch, so attach the token here.
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    const response = await fetch(`${this.baseUrl}/sessions/${sessionId}/messages/stream`, {
      method: 'POST',
      headers,
      body: JSON.stringify({ message, attachments }),
      signal
    });

    if (response.status === 401) {
      // Token missing or expired — surface it and force re-auth like the interceptor.
      this.auth.logout();
      yield {
        kind: 'error',
        code: 'unauthorized',
        message: 'Your session has expired. Please sign in again.'
      };
      return;
    }

    if (!response.ok || !response.body) {
      yield {
        kind: 'error',
        code: 'http_error',
        message: `Request failed with status ${response.status}`
      };
      return;
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';

    try {
      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });

        // SSE frames are separated by a blank line.
        let separator = buffer.indexOf('\n\n');
        while (separator !== -1) {
          const frame = buffer.slice(0, separator);
          buffer = buffer.slice(separator + 2);

          const parsed = this.parseFrame(frame);
          if (parsed) {
            yield parsed;
            if (parsed.kind === 'done') return;
          }

          separator = buffer.indexOf('\n\n');
        }
      }
    } finally {
      reader.releaseLock();
    }
  }

  private parseFrame(frame: string): AiStreamEvent | null {
    let eventName = 'message';
    const dataLines: string[] = [];

    for (const line of frame.split('\n')) {
      if (line.startsWith('event:')) {
        eventName = line.slice(6).trim();
      } else if (line.startsWith('data:')) {
        dataLines.push(line.slice(5).trim());
      }
    }

    if (dataLines.length === 0) {
      return null;
    }

    const raw = dataLines.join('\n');

    if (eventName === 'done') {
      return { kind: 'done' };
    }

    try {
      const payload = JSON.parse(raw);

      if (eventName === 'error') {
        return {
          kind: 'error',
          code: payload.code ?? 'ai_error',
          message: payload.message ?? 'Something went wrong.'
        };
      }

      return { kind: 'update', update: payload as AiChatStreamUpdate };
    } catch {
      return null;
    }
  }
}
