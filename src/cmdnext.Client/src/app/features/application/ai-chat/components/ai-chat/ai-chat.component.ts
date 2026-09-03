import { Component, ElementRef, OnInit, ViewChild, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { 
  AiChatAttachment, 
  AiChatService, 
  AiChatSession, 
  AiStreamPhase 
} from '@features/application/ai-chat/services/ai-chat.service';
import { NotificationService } from '@core/services/notification.service';
import { AuthService } from '@core/services/auth.service';
import { ActivatedRoute, Router } from '@angular/router';
import { ChatMarkdownPipe } from '@shared/pipes/chat-markdown.pipe';
import { MatDialog } from '@angular/material/dialog';
import { AiConfigComponent } from '@features/application/ai-chat/components/ai-config/ai-config.component';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatMenuModule } from '@angular/material/menu';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatListModule } from '@angular/material/list';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';

interface ChatMessageView {
  id: string;
  text: string;
  fromUser: boolean;
  timestamp: Date;
  isError?: boolean;
  attachments?: string[];
  finishReason?: string | null;
  inputTokens?: number | null;
  outputTokens?: number | null;
}

interface ChatSessionView {
  id: string;
  title: string;
  messages: ChatMessageView[];
  loaded: boolean;
  provider?: string | null;
  model?: string | null;
  spaceId?: string | null;
}

@Component({
  selector: 'app-ai-chat',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    ChatMarkdownPipe,
    MatIconModule,
    MatButtonModule,
    MatTooltipModule,
    MatMenuModule,
    MatDividerModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatListModule,
    MatSlideToggleModule,
    AiConfigComponent
  ],
  templateUrl: './ai-chat.component.html',
  styleUrls: ['./ai-chat.component.scss']
})
export class AiChatComponent implements OnInit {
  private static readonly supportedExtensions = [
    '.pdf', '.docx', '.xlsx', '.xlsm',
    '.txt', '.md', '.csv', '.json', '.xml',
    '.png', '.jpg', '.jpeg', '.gif', '.webp'
  ];
  private static readonly maxFileSizeBytes = 10 * 1024 * 1024;

  @ViewChild('messageList') messageList!: ElementRef<HTMLDivElement>;
  @ViewChild('fileInput') fileInput!: ElementRef<HTMLInputElement>;

  private dialog = inject(MatDialog);

  isTyping = false;
  isCompacting = false;
  isSending = false;
  inputControl = new FormControl('');
  searchControl = new FormControl('');
  attachedFiles: File[] = [];

  chats: ChatSessionView[] = [];
  activeChatId: string | null = null;

  private abortController: AbortController | null = null;

  /** Set when this chat was opened from a space ("Chat in this space"); scopes new sessions to it. */
  private pendingSpaceId: string | null = null;

  constructor(
    private aiChatService: AiChatService,
    private notification: NotificationService,
    private auth: AuthService,
    private router: Router,
    private route: ActivatedRoute
  ) {}

  // Initials for the user avatar, derived from their profile (falls back to email).
  get userInitials(): string {
    const user = this.auth.getCurrentUser();
    if (!user) return 'U';
    const first = (user.firstName ?? '').trim();
    const last = (user.lastName ?? '').trim();
    if (first || last) {
      return ((first[0] ?? '') + (last[0] ?? '')).toUpperCase() || 'U';
    }
    const email = (user.email ?? '').trim();
    return email ? email[0].toUpperCase() : 'U';
  }

  ngOnInit(): void {
    this.pendingSpaceId = this.route.snapshot.queryParamMap.get('spaceId');
    if (this.pendingSpaceId) {
      // Opened from a space ("Chat in this space") — always start a fresh scoped
      // session rather than resuming whatever unscoped chat was last active.
      this.loadSessions(/* selectMostRecent */ false);
    } else {
      this.loadSessions();
    }
  }

  get activeChat(): ChatSessionView | undefined {
    return this.chats.find(chat => chat.id === this.activeChatId);
  }

  get filteredChats(): ChatSessionView[] {
    const query = this.searchControl.value?.trim().toLowerCase();
    if (!query) return this.chats;
    return this.chats.filter(chat => chat.title.toLowerCase().includes(query));
  }

  loadSessions(selectMostRecent = true): void {
    this.aiChatService.getSessions().subscribe({
      next: sessions => {
        this.chats = sessions.map(s => this.toView(s));
        if (this.pendingSpaceId) {
          this.startNewChat(this.pendingSpaceId);
        } else if (selectMostRecent && this.chats.length) {
          this.selectChat(this.chats[0].id);
        } else if (!this.chats.length) {
          this.startNewChat();
        }
      },
      error: () => this.notification.showError('Could not load chat sessions.')
    });
  }

  startNewChat(spaceId?: string | null): void {
    this.aiChatService.createSession(undefined, undefined, spaceId).subscribe({
      next: session => {
        const view = this.toView(session);
        view.loaded = true;
        this.chats = [view, ...this.chats];
        this.activeChatId = view.id;
        this.inputControl.setValue('');
        this.pendingSpaceId = null;
      },
      error: () => this.notification.showError('Could not start a new chat.')
    });
  }

  selectChat(id: string): void {
    this.activeChatId = id;

    const chat = this.chats.find(c => c.id === id);
    if (!chat || chat.loaded) return;

    this.aiChatService.getSession(id).subscribe({
      next: detail => {
        chat.messages = detail.messages.map(m => ({
          id: m.id,
          text: m.content ?? '',
          fromUser: m.role === 'user',
          timestamp: m.createdOn ? new Date(m.createdOn) : new Date(),
          isError: m.isError,
          attachments: m.attachments?.length ? m.attachments : undefined,
          finishReason: m.finishReason,
          inputTokens: m.inputTokens,
          outputTokens: m.outputTokens
        }));
        chat.loaded = true;
        chat.provider = detail.provider;
        chat.model = detail.model;
        this.scrollToBottom();
      },
      error: () => this.notification.showError('Could not load this conversation.')
    });
  }

  deleteChat(id: string, event: Event): void {
    event.stopPropagation();

    this.aiChatService.deleteSession(id).subscribe({
      next: () => {
        this.chats = this.chats.filter(chat => chat.id !== id);
        if (this.activeChatId === id) {
          this.activeChatId = this.chats.length ? this.chats[0].id : null;
          if (this.activeChatId) {
            this.selectChat(this.activeChatId);
          }
        }
      },
      error: () => this.notification.showError('Could not delete this chat.')
    });
  }

  async send(): Promise<void> {
    const text = this.inputControl.value?.trim();
    if (!text || this.isSending) return;

    if (!this.activeChatId) {
      this.notification.showWarning('Start a new chat first.');
      return;
    }

    const chat = this.activeChat;
    if (!chat) return;

    if (!chat.title || chat.title === 'New chat') {
      chat.title = text.length > 40 ? `${text.slice(0, 40)}...` : text;
    }

    let attachments: AiChatAttachment[] | undefined;
    try {
      attachments = await this.encodeAttachments();
    } catch {
      this.notification.showError('Could not read the attached files.');
      return;
    }

    chat.messages.push({
      id: `local-${Date.now()}`,
      text,
      fromUser: true,
      timestamp: new Date(),
      attachments: attachments?.map(a => a.fileName)
    });

    this.inputControl.setValue('');
    this.attachedFiles = [];
    this.scrollToBottom();

    this.isSending = true;
    this.isTyping = true;
    this.abortController = new AbortController();

    // Bring the "Thinking…" bubble into view now that it has been rendered.
    this.scrollToBottom();

    const assistant: ChatMessageView = {
      id: `local-assistant-${Date.now()}`,
      text: '',
      fromUser: false,
      timestamp: new Date()
    };
    let assistantAdded = false;

    try {
      const stream = this.aiChatService.streamMessage(
        chat.id,
        text,
        attachments,
        this.abortController.signal
      );

      for await (const event of stream) {
        if (event.kind === 'error') {
          this.notification.showError(event.message);
          assistant.text = event.message;
          assistant.isError = true;
          if (!assistantAdded) {
            chat.messages.push(assistant);
            assistantAdded = true;
          }
          break;
        }

        if (event.kind === 'done') {
          break;
        }

        const update = event.update;

        this.isCompacting = update.phase === AiStreamPhase.Compacting;

        if (update.textDelta) {
          if (!assistantAdded) {
            chat.messages.push(assistant);
            assistantAdded = true;
            this.isTyping = false;
          }
          assistant.text += update.textDelta;
          assistant.finishReason = update.finishReason;
          assistant.inputTokens = update.usage?.inputTokens;
          assistant.outputTokens = update.usage?.outputTokens;
          this.scrollToBottom();
        }
      }
    } catch {
      this.notification.showError('The connection to the assistant was lost.');
    } finally {
      this.isSending = false;
      this.isTyping = false;
      this.isCompacting = false;
      this.abortController = null;
      this.scrollToBottom();
    }
  }

  onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.send();
    }
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files) return;

    for (const file of Array.from(input.files)) {
      this.addFile(file);
    }

    input.value = '';
  }

  /** Attach an image (or other supported file) pasted from the clipboard. */
  onPaste(event: ClipboardEvent): void {
    const items = event.clipboardData?.items;
    if (!items) return;

    let attachedAny = false;
    for (const item of Array.from(items)) {
      if (item.kind !== 'file') continue;
      const file = item.getAsFile();
      if (!file) continue;

      // Clipboard images often have no name (e.g. a screenshot) — give them one.
      const named = file.name && file.name.includes('.')
        ? file
        : new File([file], this.generatePastedName(file.type), { type: file.type });

      if (this.addFile(named)) {
        attachedAny = true;
      }
    }

    // Only swallow the paste if we actually took an image, so pasting text still works.
    if (attachedAny) {
      event.preventDefault();
      this.notification.showInfo('Image attached from clipboard.');
    }
  }

  /** Validates and adds one file to the attachment list. Returns true if added. */
  private addFile(file: File): boolean {
    const dot = file.name.lastIndexOf('.');
    const extension = dot >= 0 ? file.name.slice(dot).toLowerCase() : '';

    if (!AiChatComponent.supportedExtensions.includes(extension)) {
      this.notification.showWarning(
        `${file.name || 'File'} is not supported. Allowed types: ${AiChatComponent.supportedExtensions.join(', ')}`
      );
      return false;
    }

    if (file.size > AiChatComponent.maxFileSizeBytes) {
      this.notification.showWarning(`${file.name || 'File'} is too large. Maximum size is 10 MB.`);
      return false;
    }

    this.attachedFiles = [...this.attachedFiles, file];
    return true;
  }

  private generatePastedName(mimeType: string): string {
    const ext = mimeType === 'image/png' ? '.png'
      : mimeType === 'image/jpeg' ? '.jpg'
      : mimeType === 'image/gif' ? '.gif'
      : mimeType === 'image/webp' ? '.webp'
      : '.png';
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');
    return `pasted-${stamp}${ext}`;
  }

  removeFile(index: number): void {
    this.attachedFiles = this.attachedFiles.filter((_, i) => i !== index);
  }

  private async encodeAttachments(): Promise<AiChatAttachment[] | undefined> {
    if (!this.attachedFiles.length) return undefined;

    return Promise.all(
      this.attachedFiles.map(async file => ({
        fileName: file.name,
        content: await this.toBase64(file)
      }))
    );
  }

  private toBase64(file: File): Promise<string> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => {
        const result = reader.result as string;
        resolve(result.slice(result.indexOf(',') + 1));
      };
      reader.onerror = () => reject(reader.error);
      reader.readAsDataURL(file);
    });
  }

  private toView(session: AiChatSession): ChatSessionView {
    return {
      id: session.id,
      title: session.title || 'New chat',
      messages: [],
      loaded: false,
      provider: session.provider,
      model: session.model,
      spaceId: session.spaceId
    };
  }

  private scrollToBottom(): void {
    setTimeout(() => {
      if (this.messageList) {
        this.messageList.nativeElement.scrollTop = this.messageList.nativeElement.scrollHeight;
      }
    }, 50);
  }

  goHome(): void {
    this.router.navigate(['/dashboard']);
  }

  logout(): void {
    // Cancel any in-flight stream before leaving.
    this.abortController?.abort();
    this.auth.logout(); // clears token + navigates to /auth/login
  }

  openAiConfig(): void {
    this.dialog.open(AiConfigComponent, {
      width: '700px',
      height: '70vh',
      maxHeight: '80vh',
      panelClass: 'ai-config-dialog-panel'
    });
  }
}
