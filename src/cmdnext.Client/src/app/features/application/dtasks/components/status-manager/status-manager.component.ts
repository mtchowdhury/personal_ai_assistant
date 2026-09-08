import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { DTaskService, TaskStatus } from '@features/application/dtasks/services/dtask.service';
import { NotificationService } from '@core/services/notification.service';
import { ConfirmDialogService } from '@core/services/confirm-dialog.service';
import { LoadingSpinnerComponent } from '@core/components/loading-spinner/loading-spinner.component';

const DEFAULT_COLOR = '#6b7280';

@Component({
  selector: 'app-status-manager',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LoadingSpinnerComponent],
  templateUrl: './status-manager.component.html',
  styleUrls: ['./status-manager.component.scss']
})
export class StatusManagerComponent implements OnInit {
  statuses: TaskStatus[] = [];
  isLoading = true;

  // New-status form
  newName = '';
  newColor = DEFAULT_COLOR;
  newIsDone = false;
  isSaving = false;

  /** Id of the row currently open for inline editing. */
  editingId: string | null = null;
  editName = '';
  editColor = DEFAULT_COLOR;
  editIsDone = false;

  constructor(
    private tasks: DTaskService,
    private notification: NotificationService,
    private confirmDialog: ConfirmDialogService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading = true;
    this.tasks.getStatuses().subscribe({
      next: (list) => {
        this.statuses = list;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.notification.showError('Could not load statuses.');
      }
    });
  }

  create(): void {
    const name = this.newName.trim();
    if (!name || this.isSaving) return;

    this.isSaving = true;
    this.tasks.createStatus({ name, color: this.newColor, isDone: this.newIsDone }).subscribe({
      next: () => {
        this.newName = '';
        this.newColor = DEFAULT_COLOR;
        this.newIsDone = false;
        this.isSaving = false;
        this.notification.showSuccess('Status added.');
        this.load();
      },
      error: (err) => {
        this.isSaving = false;
        this.notification.showError(err?.error?.message || 'Could not add the status.');
      }
    });
  }

  startEdit(status: TaskStatus): void {
    this.editingId = status.id;
    this.editName = status.name;
    this.editColor = status.color || DEFAULT_COLOR;
    this.editIsDone = status.isDone;
  }

  cancelEdit(): void {
    this.editingId = null;
  }

  saveEdit(status: TaskStatus): void {
    const name = this.editName.trim();
    if (!name) return;

    this.tasks.updateStatus(status.id, {
      name,
      color: this.editColor,
      isDone: this.editIsDone
    }).subscribe({
      next: () => {
        this.editingId = null;
        this.notification.showSuccess('Status updated.');
        this.load();
      },
      error: (err) => this.notification.showError(err?.error?.message || 'Could not save the status.')
    });
  }

  /** Swaps a status with its neighbour and persists the whole order. */
  move(status: TaskStatus, direction: -1 | 1): void {
    const index = this.statuses.findIndex(s => s.id === status.id);
    const target = index + direction;
    if (index < 0 || target < 0 || target >= this.statuses.length) return;

    const reordered = [...this.statuses];
    [reordered[index], reordered[target]] = [reordered[target], reordered[index]];
    this.statuses = reordered;

    this.tasks.reorderStatuses(reordered.map(s => s.id)).subscribe({
      next: () => {},
      error: () => {
        this.notification.showError('Could not reorder the statuses.');
        this.load();
      }
    });
  }

  async remove(status: TaskStatus): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      title: 'Delete status',
      message: `Delete the "${status.name}" status? This cannot be undone.`,
      confirmLabel: 'Delete',
      danger: true
    });
    if (!ok) return;

    this.tasks.deleteStatus(status.id).subscribe({
      next: () => {
        this.notification.showSuccess('Status deleted.');
        this.load();
      },
      error: (err) => this.notification.showError(err?.error?.message || 'Could not delete this status.')
    });
  }

  goBack(): void {
    this.router.navigate(['/dtasks']);
  }
}
