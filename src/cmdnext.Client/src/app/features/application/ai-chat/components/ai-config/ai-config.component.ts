import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule, DecimalPipe, DatePipe } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule, FormsModule } from '@angular/forms';
import { MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { forkJoin } from 'rxjs';
import {
  AiConfigService,
  AiProvider,
  AiSettings,
  AiUsageSummary,
  AddProviderRequest,
  UpdateProviderRequest,
  UpdateSettingsRequest
} from '@features/application/ai-chat/services/ai-config.service';
import { NotificationService } from '@core/services/notification.service';

@Component({
  selector: 'app-ai-config',
  standalone: true,
  imports: [
    CommonModule,
    DecimalPipe,
    DatePipe,
    FormsModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatInputModule,
    MatFormFieldModule,
    MatSelectModule,
    MatIconModule,
    MatDividerModule,
    MatSlideToggleModule,
    MatTabsModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
    MatButtonToggleModule
  ],
  templateUrl: './ai-config.component.html',
  styleUrls: ['./ai-config.component.scss']
})
export class AiConfigComponent implements OnInit {
  private dialogRef = inject(MatDialogRef<AiConfigComponent>);
  private fb = inject(FormBuilder);
  private configService = inject(AiConfigService);
  private notification = inject(NotificationService);

  providers = signal<AiProvider[]>([]);
  settings = signal<AiSettings | null>(null);
  usage = signal<AiUsageSummary | null>(null);

  isLoading = signal(true);
  isSaving = signal(false);
  isUsageLoading = signal(false);
  error = signal<string | null>(null);

  /** Provider row currently open for editing, by id. */
  editingId = signal<string | null>(null);
  showAddForm = signal(false);
  usageDays = signal(30);

  addProviderForm: FormGroup;
  editProviderForm: FormGroup;
  settingsForm: FormGroup;
  selectedProviderForModel = signal('mistral');

  availableProviders = this.configService.getAvailableProviders();
  availableModels = computed(() => this.configService.getAvailableModels(this.selectedProviderForModel()));

  /** Providers the user has not configured yet — the add form offers only these. */
  addableProviders = computed(() => {
    const taken = new Set(this.providers().map(p => p.provider.toLowerCase()));
    return this.availableProviders.filter(p => !taken.has(p.name.toLowerCase()));
  });

  maxDailyTokens = computed(() => {
    const daily = this.usage()?.daily ?? [];
    return daily.reduce((max, d) => Math.max(max, d.totalTokens), 0);
  });

  /** Share of total tokens per model, for the breakdown bars. */
  modelShare = computed(() => {
    const u = this.usage();
    if (!u || u.totalTokens === 0) return [];
    return u.byModel.map(m => ({
      ...m,
      percent: Math.round((m.totalTokens / u.totalTokens) * 100)
    }));
  });

  constructor() {
    this.addProviderForm = this.fb.group({
      provider: ['mistral', Validators.required],
      apiKey: ['', [Validators.required, Validators.minLength(10)]],
      endpoint: ['']
    });

    // API key is optional when editing: blank means "keep the existing key".
    this.editProviderForm = this.fb.group({
      apiKey: ['', Validators.minLength(10)],
      endpoint: [''],
      isActive: [true]
    });

    this.settingsForm = this.fb.group({
      defaultProvider: ['', Validators.required],
      defaultModel: ['', Validators.required],
      temperature: [0.7, [Validators.required, Validators.min(0), Validators.max(1)]],
      maxOutputTokens: [4096, [Validators.required, Validators.min(256), Validators.max(32768)]],
      isChatEnabled: [true]
    });
  }

  ngOnInit(): void {
    this.loadData();
  }

  get addProviderControls() {
    return this.addProviderForm.controls;
  }

  get editProviderControls() {
    return this.editProviderForm.controls;
  }

  get settingsControls() {
    return this.settingsForm.controls;
  }

  get settingsFormDisabled() {
    return this.settingsForm.invalid || this.isSaving();
  }

  loadData(): void {
    this.isLoading.set(true);
    this.error.set(null);

    forkJoin([
      this.configService.getProviders(),
      this.configService.getSettings(),
      this.configService.getUsage(this.usageDays())
    ]).subscribe({
      next: ([providers, settings, usage]) => {
        this.providers.set(providers);
        this.settings.set(settings);
        this.usage.set(usage);

        this.settingsForm.patchValue({
          defaultProvider: settings.defaultProvider || 'mistral',
          defaultModel: settings.defaultModel || '',
          temperature: settings.temperature ?? 0.7,
          maxOutputTokens: settings.maxOutputTokens ?? 4096,
          isChatEnabled: settings.isChatEnabled ?? true
        });

        if (settings.defaultProvider) {
          this.selectedProviderForModel.set(settings.defaultProvider);
        }

        // Open the add form straight away when there is nothing configured.
        this.showAddForm.set(providers.length === 0);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.error.set('Failed to load your AI configuration.');
      }
    });
  }

  reloadUsage(days: number): void {
    this.usageDays.set(days);
    this.isUsageLoading.set(true);
    this.configService.getUsage(days).subscribe({
      next: usage => {
        this.usage.set(usage);
        this.isUsageLoading.set(false);
      },
      error: () => {
        this.isUsageLoading.set(false);
        this.notification.showError('Could not load usage statistics.');
      }
    });
  }

  toggleAddForm(): void {
    const next = !this.showAddForm();
    this.showAddForm.set(next);
    if (next) {
      this.editingId.set(null);
      const first = this.addableProviders()[0]?.name ?? 'mistral';
      this.addProviderForm.reset({ provider: first, apiKey: '', endpoint: '' });
    }
  }

  addProvider(): void {
    if (this.addProviderForm.invalid) {
      this.addProviderForm.markAllAsTouched();
      return;
    }

    const request: AddProviderRequest = {
      provider: this.addProviderForm.value.provider,
      apiKey: this.addProviderForm.value.apiKey.trim(),
      endpoint: this.addProviderForm.value.endpoint?.trim() || undefined
    };

    this.isSaving.set(true);
    this.configService.addProvider(request).subscribe({
      next: saved => {
        // The API upserts, so replace a matching row instead of appending a duplicate.
        this.providers.update(ps => {
          const i = ps.findIndex(p => p.id === saved.id);
          return i >= 0 ? [...ps.slice(0, i), saved, ...ps.slice(i + 1)] : [...ps, saved];
        });
        this.addProviderForm.reset({ provider: 'mistral', apiKey: '', endpoint: '' });
        this.showAddForm.set(false);
        this.notification.showSuccess('Provider saved.');
        this.isSaving.set(false);
      },
      error: err => {
        this.notification.showError(this.messageFor(err, 'Could not save the provider.'));
        this.isSaving.set(false);
      }
    });
  }

  startEdit(provider: AiProvider): void {
    this.showAddForm.set(false);
    this.editingId.set(provider.id);
    this.editProviderForm.reset({
      apiKey: '',
      endpoint: provider.endpoint ?? '',
      isActive: provider.isActive
    });
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.editProviderForm.reset({ apiKey: '', endpoint: '', isActive: true });
  }

  saveEdit(provider: AiProvider): void {
    if (this.editProviderForm.invalid) {
      this.editProviderForm.markAllAsTouched();
      return;
    }

    const value = this.editProviderForm.value;
    const request: UpdateProviderRequest = {
      apiKey: value.apiKey?.trim() || undefined,
      endpoint: value.endpoint?.trim() || undefined,
      isActive: value.isActive
    };

    this.isSaving.set(true);
    this.configService.updateProvider(provider.id, request).subscribe({
      next: saved => {
        this.providers.update(ps => ps.map(p => (p.id === saved.id ? saved : p)));
        this.editingId.set(null);
        this.notification.showSuccess(
          request.apiKey ? 'API key updated.' : 'Provider updated.'
        );
        this.isSaving.set(false);
      },
      error: err => {
        this.notification.showError(this.messageFor(err, 'Could not update the provider.'));
        this.isSaving.set(false);
      }
    });
  }

  deleteProvider(provider: AiProvider): void {
    if (!confirm(`Remove the ${this.getProviderDisplayName(provider.provider)} key? Chat will stop working if it is the active provider.`)) {
      return;
    }

    this.isSaving.set(true);
    this.configService.deleteProvider(provider.id).subscribe({
      next: () => {
        this.providers.update(ps => ps.filter(p => p.id !== provider.id));
        if (this.editingId() === provider.id) this.editingId.set(null);
        this.notification.showSuccess('Provider removed.');
        this.isSaving.set(false);
      },
      error: err => {
        this.notification.showError(this.messageFor(err, 'Could not remove the provider.'));
        this.isSaving.set(false);
      }
    });
  }

  saveSettings(): void {
    if (this.settingsForm.invalid) {
      this.settingsForm.markAllAsTouched();
      return;
    }

    const formValue = this.settingsForm.value;
    const request: UpdateSettingsRequest = {
      defaultProvider: formValue.defaultProvider,
      defaultModel: formValue.defaultModel,
      temperature: formValue.temperature,
      maxOutputTokens: formValue.maxOutputTokens,
      isChatEnabled: formValue.isChatEnabled
    };

    this.isSaving.set(true);
    this.configService.updateSettings(request).subscribe({
      next: settings => {
        this.settings.set(settings);
        this.notification.showSuccess('Settings saved.');
        this.isSaving.set(false);
      },
      error: err => {
        this.notification.showError(this.messageFor(err, 'Could not save settings.'));
        this.isSaving.set(false);
      }
    });
  }

  selectProviderForModel(provider: string): void {
    this.selectedProviderForModel.set(provider);

    // Drop a model that isn't offered by the newly selected provider, otherwise
    // mat-select holds a value with no matching option and renders empty.
    const current = this.settingsForm.get('defaultModel')?.value;
    const stillValid = this.configService
      .getAvailableModels(provider)
      .some(m => m.name === current);
    if (!stillValid) {
      this.settingsForm.patchValue({ defaultModel: '' });
    }
  }

  /** True when the default provider has no matching key configured. */
  hasCredentialForDefault = computed(() => {
    const preferred = this.settings()?.defaultProvider?.toLowerCase();
    if (!preferred) return true;
    return this.providers().some(p => p.provider.toLowerCase() === preferred && p.isActive);
  });

  close(): void {
    this.dialogRef.close();
  }

  getProviderDisplayName(providerName: string): string {
    return this.availableProviders.find(p => p.name === providerName)?.displayName ?? providerName;
  }

  getModelDisplayName(modelName: string): string {
    for (const p of this.availableProviders) {
      const hit = this.configService.getAvailableModels(p.name).find(m => m.name === modelName);
      if (hit) return hit.displayName;
    }
    return modelName;
  }

  providerIcon(provider: string): string {
    return provider.toLowerCase() === 'anthropic' ? 'auto_awesome' : 'smart_toy';
  }

  /** Compact token display: 1234 -> 1.2k, 1234567 -> 1.2M. */
  formatTokens(value: number | undefined | null): string {
    const n = value ?? 0;
    if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(1)}M`;
    if (n >= 1_000) return `${(n / 1_000).toFixed(1)}k`;
    return `${n}`;
  }

  barHeight(tokens: number): number {
    const max = this.maxDailyTokens();
    if (max === 0) return 0;
    // Floor at 2% so days with light-but-nonzero usage stay visible.
    return Math.max(2, Math.round((tokens / max) * 100));
  }

  private messageFor(err: unknown, fallback: string): string {
    const e = err as { error?: { message?: string }; status?: number };
    if (e?.error?.message) return e.error.message;
    if (e?.status === 404) return 'That provider no longer exists. Reload and try again.';
    return fallback;
  }
}
