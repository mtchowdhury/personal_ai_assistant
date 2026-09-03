import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormControl, Validators, ReactiveFormsModule, FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { MatListModule } from '@angular/material/list';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { forkJoin } from 'rxjs';
import { AiConfigService, AiProvider, AiSettings, AddProviderRequest, UpdateSettingsRequest } from '@features/application/ai-chat/services/ai-config.service';
import { NotificationService } from '@core/services/notification.service';

@Component({
  selector: 'app-ai-config',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatInputModule,
    MatFormFieldModule,
    MatSelectModule,
    MatIconModule,
    MatDividerModule,
    MatListModule,
    MatSlideToggleModule,
    MatSnackBarModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './ai-config.component.html',
  styleUrls: ['./ai-config.component.scss']
})
export class AiConfigComponent implements OnInit {
  private dialogRef = inject(MatDialogRef<AiConfigComponent>);
  private snackBar = inject(MatSnackBar);
  private fb = inject(FormBuilder);
  private configService = inject(AiConfigService);
  private notification = inject(NotificationService);

  // State
  providers = signal<AiProvider[]>([]);
  settings = signal<AiSettings | null>(null);
  isLoading = signal(true);
  error = signal<string | null>(null);

  // Form for adding provider
  addProviderForm: FormGroup;
  settingsForm: FormGroup;
  selectedProviderForModel = signal('mistral');

  // Computed
  availableProviders = this.configService.getAvailableProviders();
  availableModels = computed(() => this.configService.getAvailableModels(this.selectedProviderForModel()));

  constructor() {
    // Initialize forms
    this.addProviderForm = this.fb.group({
      provider: ['mistral', Validators.required],
      apiKey: ['', [Validators.required, Validators.minLength(10)]],
      endpoint: ['']
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

  // Form getters for template
  get addProviderControls() {
    return this.addProviderForm.controls;
  }

  get settingsControls() {
    return this.settingsForm.controls;
  }

  get addProviderFormDisabled() {
    return this.addProviderForm.invalid || this.isLoading();
  }

  get settingsFormDisabled() {
    return this.settingsForm.invalid || this.isLoading();
  }

  loadData(): void {
    this.isLoading.set(true);
    this.error.set(null);

    forkJoin([
      this.configService.getProviders(),
      this.configService.getSettings()
    ]).subscribe({
      next: ([providers, settings]) => {
        this.providers.set(providers);
        this.settings.set(settings);
        
        // Populate settings form
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
        this.isLoading.set(false);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.error.set('Failed to load providers or settings');
        this.notification.showError('Failed to load configuration. Please try again.');
      }
    });
  }

  addProvider(): void {
    if (this.addProviderForm.invalid) {
      this.addProviderForm.markAllAsTouched();
      return;
    }

    const request: AddProviderRequest = {
      provider: this.addProviderForm.value.provider,
      apiKey: this.addProviderForm.value.apiKey,
      endpoint: this.addProviderForm.value.endpoint || undefined
    };

    this.isLoading.set(true);
    this.configService.addProvider(request).subscribe({
      next: (provider) => {
        this.providers.update(ps => [...ps, provider]);
        this.addProviderForm.reset({ provider: 'mistral', apiKey: '', endpoint: '' });
        this.notification.showSuccess('AI provider added successfully');
        this.isLoading.set(false);
      },
      error: (err) => {
        if (err.status === 400) {
          this.notification.showError('Invalid request. Please check your inputs.');
        } else {
          this.notification.showError('Failed to add provider. Please check your API key and try again.');
        }
        this.isLoading.set(false);
      }
    });
  }

  saveSettings(): void {
    if (this.settingsForm.invalid) {
      this.settingsForm.markAllAsTouched();
      return;
    }

    if (!this.settings()) return;

    const formValue = this.settingsForm.value;
    const request: UpdateSettingsRequest = {
      defaultProvider: formValue.defaultProvider,
      defaultModel: formValue.defaultModel,
      temperature: formValue.temperature,
      maxOutputTokens: formValue.maxOutputTokens,
      isChatEnabled: formValue.isChatEnabled
    };

    this.isLoading.set(true);
    this.configService.updateSettings(request).subscribe({
      next: (settings) => {
        this.settings.set(settings);
        this.notification.showSuccess('Settings saved successfully');
        this.isLoading.set(false);
      },
      error: (err) => {
        this.notification.showError('Failed to save settings. Please try again.');
        this.isLoading.set(false);
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

  close(): void {
    this.dialogRef.close();
  }

  getProviderDisplayName(providerName: string): string {
    const provider = this.availableProviders.find(p => p.name === providerName);
    return provider ? provider.displayName : providerName;
  }

  getModelDisplayName(modelName: string): string {
    const models = this.configService.getAvailableModels(this.selectedProviderForModel());
    const model = models.find(m => m.name === modelName);
    return model ? model.displayName : modelName;
  }

  setDefaultProvider(providerId: string): void {
    this.settings.update(s => {
      if (s) {
        const provider = this.providers().find(p => p.id === providerId);
        return { ...s, defaultProvider: provider?.provider };
      }
      return s;
    });
  }

  setDefaultModel(modelName: string): void {
    this.settings.update(s => {
      if (s) {
        return { ...s, defaultModel: modelName };
      }
      return s;
    });
  }
}
