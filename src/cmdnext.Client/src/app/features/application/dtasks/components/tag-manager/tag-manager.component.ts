import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { DTaskService, TaskTag } from '@features/application/dtasks/services/dtask.service';
import { NotificationService } from '@core/services/notification.service';
import { ConfirmDialogService } from '@core/services/confirm-dialog.service';
import { LoadingSpinnerComponent } from '@core/components/loading-spinner/loading-spinner.component';

const DEFAULT_COLOR = '#6b7280';

@Component({
  selector: 'app-tag-manager',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LoadingSpinnerComponent],
  templateUrl: './tag-manager.component.html',
  styleUrls: ['./tag-manager.component.scss']
})
export class TagManagerComponent implements OnInit {
  tags: TaskTag[] = [];
  isLoading = true;

  newName = '';
  newColor = DEFAULT_COLOR;
  isSaving = false;

  editingId: string | null = null;
  editName = '';
  editColor = DEFAULT_COLOR;

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
    this.tasks.getTags().subscribe({
      next: (list) => {
        this.tags = list;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.notification.showError('Could not load tags.');
      }
    });
  }

  create(): void {
    const name = this.newName.trim();
    if (!name || this.isSaving) return;

    this.isSaving = true;
    this.tasks.createTag({ name, color: this.newColor }).subscribe({
      next: () => {
        this.newName = '';
        this.newColor = DEFAULT_COLOR;
        this.isSaving = false;
        this.notification.showSuccess('Tag added.');
        this.load();
      },
      error: (err) => {
        this.isSaving = false;
        this.notification.showError(err?.error?.message || 'Could not add the tag.');
      }
    });
  }

  startEdit(tag: TaskTag): void {
    this.editingId = tag.id;
    this.editName = tag.name;
    this.editColor = tag.color || DEFAULT_COLOR;
  }

  cancelEdit(): void {
    this.editingId = null;
  }

  saveEdit(tag: TaskTag): void {
    const name = this.editName.trim();
    if (!name) return;

    this.tasks.updateTag(tag.id, { name, color: this.editColor }).subscribe({
      next: () => {
        this.editingId = null;
        // A rename rewrites the tag on every task carrying it, so reload the counts.
        this.notification.showSuccess('Tag updated.');
        this.load();
      },
      error: (err) => this.notification.showError(err?.error?.message || 'Could not save the tag.')
    });
  }

  async remove(tag: TaskTag): Promise<void> {
    const extra = tag.taskCount > 0
      ? ` It will be removed from ${tag.taskCount} task(s).`
      : '';
    const ok = await this.confirmDialog.confirm({
      title: 'Delete tag',
      message: `Delete the "${tag.name}" tag?${extra} This cannot be undone.`,
      confirmLabel: 'Delete',
      danger: true
    });
    if (!ok) return;

    this.tasks.deleteTag(tag.id).subscribe({
      next: () => {
        this.notification.showSuccess('Tag deleted.');
        this.load();
      },
      error: () => this.notification.showError('Could not delete this tag.')
    });
  }

  goBack(): void {
    this.router.navigate(['/dtasks']);
  }
}
