import { Injectable } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';
import { ConfirmDialogComponent, ConfirmDialogData } from '@core/components/confirm-dialog/confirm-dialog.component';

export interface ConfirmOptions {
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  danger?: boolean;
}

export interface PromptOptions {
  title: string;
  message: string;
  initialValue?: string;
  placeholder?: string;
  confirmLabel?: string;
  cancelLabel?: string;
}

@Injectable({ providedIn: 'root' })
export class ConfirmDialogService {
  constructor(private dialog: MatDialog) {}

  /** Resolves true if confirmed, false if cancelled/dismissed. */
  async confirm(options: ConfirmOptions): Promise<boolean> {
    const data: ConfirmDialogData = { ...options };
    const ref = this.dialog.open(ConfirmDialogComponent, { data, width: '420px', autoFocus: false });
    const result = await firstValueFrom(ref.afterClosed());
    return result === true;
  }

  /** Resolves the entered string if confirmed, or null if cancelled/dismissed. */
  async prompt(options: PromptOptions): Promise<string | null> {
    const data: ConfirmDialogData = {
      title: options.title,
      message: options.message,
      confirmLabel: options.confirmLabel,
      cancelLabel: options.cancelLabel,
      promptValue: options.initialValue ?? '',
      promptPlaceholder: options.placeholder
    };
    const ref = this.dialog.open(ConfirmDialogComponent, { data, width: '420px' });
    const result = await firstValueFrom(ref.afterClosed());
    return typeof result === 'string' ? result : null;
  }
}
