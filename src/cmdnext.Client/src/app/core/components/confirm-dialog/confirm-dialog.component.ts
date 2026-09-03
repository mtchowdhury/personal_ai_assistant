import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';

export interface ConfirmDialogData {
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  danger?: boolean;
  /** When set, renders a text input pre-filled with this value and resolves with the entered string. */
  promptValue?: string;
  promptPlaceholder?: string;
}

@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, MatDialogModule],
  templateUrl: './confirm-dialog.component.html',
  styleUrls: ['./confirm-dialog.component.scss']
})
export class ConfirmDialogComponent {
  inputValue: string;

  constructor(
    private dialogRef: MatDialogRef<ConfirmDialogComponent, string | boolean>,
    @Inject(MAT_DIALOG_DATA) public data: ConfirmDialogData
  ) {
    this.inputValue = data.promptValue ?? '';
  }

  get isPrompt(): boolean {
    return this.data.promptValue !== undefined;
  }

  confirm(): void {
    this.dialogRef.close(this.isPrompt ? this.inputValue : true);
  }

  cancel(): void {
    this.dialogRef.close(this.isPrompt ? undefined : false);
  }
}
