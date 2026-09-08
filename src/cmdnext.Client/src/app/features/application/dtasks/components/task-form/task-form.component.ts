import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import {
  DTaskService, TaskStatus, TaskTag, TaskListItem, Subtask,
  TaskPriority, TaskCategory, TaskTimeOfDay,
  PRIORITIES, CATEGORIES, TIMES_OF_DAY
} from '@features/application/dtasks/services/dtask.service';
import { NotificationService } from '@core/services/notification.service';
import { ConfirmDialogService } from '@core/services/confirm-dialog.service';
import { LoadingSpinnerComponent } from '@core/components/loading-spinner/loading-spinner.component';

/** Views a task can be opened from, so saving returns where the user came from. */
type ReturnView = 'calendar' | 'board' | 'list';

@Component({
  selector: 'app-task-form',
  standalone: true,
  imports: [CommonModule, FormsModule, LoadingSpinnerComponent],
  templateUrl: './task-form.component.html',
  styleUrls: ['./task-form.component.scss']
})
export class TaskFormComponent implements OnInit {
  readonly priorities = PRIORITIES;
  readonly categories = CATEGORIES;
  readonly timesOfDay = TIMES_OF_DAY;

  taskId: string | null = null;

  title = '';
  notes = '';
  category: TaskCategory = 'Task';
  priority: TaskPriority = 'Medium';
  scheduledOn = '';
  timeOfDay: TaskTimeOfDay | '' = '';
  approxDurationMinutes: number | null = null;
  statusId = '';
  parentId = '';
  selectedTags: string[] = [];

  statuses: TaskStatus[] = [];
  tags: TaskTag[] = [];
  /** Candidate parents — top-level tasks only, since nesting is one level deep. */
  parentOptions: TaskListItem[] = [];

  /** Existing subtasks, managed inline while editing. */
  subtasks: Subtask[] = [];
  newSubtaskTitle = '';
  isAddingSubtask = false;

  isLoading = true;
  isSaving = false;

  /** Where to go on save/cancel, and the month to restore on the calendar. */
  private returnView: ReturnView = 'calendar';
  private returnYear: number | null = null;
  private returnMonth: number | null = null;

  constructor(
    private tasks: DTaskService,
    private notification: NotificationService,
    private confirmDialog: ConfirmDialogService,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  get isEdit(): boolean {
    return this.taskId !== null;
  }

  ngOnInit(): void {
    this.taskId = this.route.snapshot.paramMap.get('id');

    const query = this.route.snapshot.queryParamMap;

    // The calendar's per-day "+" pre-fills the date; a parent can be pre-set the same way.
    const queryDate = query.get('date');
    if (queryDate) this.scheduledOn = queryDate;
    const queryParent = query.get('parentId');
    if (queryParent) {
      this.parentId = queryParent;
      this.category = 'Subtask';
    }

    const from = query.get('from');
    if (from === 'board' || from === 'list' || from === 'calendar') this.returnView = from;
    const year = Number(query.get('year'));
    const month = Number(query.get('month'));
    if (year > 0 && month > 0) {
      this.returnYear = year;
      this.returnMonth = month;
    }

    forkJoin({
      statuses: this.tasks.getStatuses(),
      tags: this.tasks.getTags(),
      parents: this.tasks.getTasks({ topLevelOnly: true, take: 500 })
    }).subscribe({
      next: ({ statuses, tags, parents }) => {
        this.statuses = statuses;
        this.tags = tags;
        this.parentOptions = parents;

        if (!this.statusId) {
          const firstOpen = statuses.find(s => !s.isDone) ?? statuses[0];
          this.statusId = firstOpen?.id ?? '';
        }

        if (this.taskId) this.loadTask(this.taskId);
        else this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.notification.showError('Could not load the form.');
      }
    });
  }

  private loadTask(id: string): void {
    this.tasks.getTask(id).subscribe({
      next: (task) => {
        this.title = task.title;
        this.notes = task.notes ?? '';
        this.category = task.category;
        this.priority = task.priority;
        this.scheduledOn = task.scheduledOn ? task.scheduledOn.substring(0, 10) : '';
        this.timeOfDay = task.timeOfDay ?? '';
        this.approxDurationMinutes = task.approxDurationMinutes ?? null;
        this.statusId = task.statusId;
        this.parentId = task.parentId ?? '';
        this.selectedTags = [...task.tags];
        this.subtasks = task.subtasks;

        // A task cannot be its own parent.
        this.parentOptions = this.parentOptions.filter(p => p.id !== id);
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.notification.showError('Could not load this task.');
        this.goBack();
      }
    });
  }

  isTagSelected(name: string): boolean {
    return this.selectedTags.includes(name);
  }

  toggleTag(name: string): void {
    this.selectedTags = this.isTagSelected(name)
      ? this.selectedTags.filter(t => t !== name)
      : [...this.selectedTags, name];
  }

  // ---- Subtasks ----

  get subtaskProgress(): string {
    if (this.subtasks.length === 0) return '';
    return `${this.subtasks.filter(s => s.isDone).length}/${this.subtasks.length}`;
  }

  toggleSubtask(subtask: Subtask): void {
    this.tasks.toggleDone(subtask.id).subscribe({
      next: (updated) => {
        subtask.statusId = updated.statusId;
        subtask.statusName = updated.statusName;
        subtask.statusColor = updated.statusColor ?? null;
        subtask.isDone = updated.isDone;
      },
      error: () => this.notification.showError('Could not update the subtask.')
    });
  }

  addSubtask(): void {
    const title = this.newSubtaskTitle.trim();
    if (!title || !this.taskId || this.isAddingSubtask) return;

    this.isAddingSubtask = true;
    this.tasks.createTask({
      title,
      category: 'Subtask',
      parentId: this.taskId,
      // A subtask inherits the parent's date so it lands on the same calendar day.
      scheduledOn: this.scheduledOn || null,
      tags: []
    }).subscribe({
      next: (created) => {
        this.subtasks = [...this.subtasks, {
          id: created.id,
          title: created.title,
          statusId: created.statusId,
          statusName: created.statusName,
          statusColor: created.statusColor ?? null,
          isDone: created.isDone,
          priority: created.priority,
          scheduledOn: created.scheduledOn ?? null,
          sortOrder: created.sortOrder
        }];
        this.newSubtaskTitle = '';
        this.isAddingSubtask = false;
      },
      error: () => {
        this.isAddingSubtask = false;
        this.notification.showError('Could not add the subtask.');
      }
    });
  }

  async removeSubtask(subtask: Subtask, event: Event): Promise<void> {
    event.stopPropagation();

    const ok = await this.confirmDialog.confirm({
      title: 'Delete subtask',
      message: `Delete "${subtask.title}"? This cannot be undone.`,
      confirmLabel: 'Delete',
      danger: true
    });
    if (!ok) return;

    this.tasks.deleteTask(subtask.id).subscribe({
      next: () => (this.subtasks = this.subtasks.filter(s => s.id !== subtask.id)),
      error: () => this.notification.showError('Could not delete the subtask.')
    });
  }

  // ---- Save / delete ----

  get canSave(): boolean {
    return this.title.trim().length > 0 && !!this.statusId && !this.isSaving;
  }

  save(): void {
    if (!this.canSave) return;
    this.isSaving = true;

    if (this.isEdit) {
      this.tasks.updateTask(this.taskId!, {
        title: this.title.trim(),
        notes: this.notes.trim(),
        category: this.category,
        priority: this.priority,
        scheduledOn: this.scheduledOn || null,
        clearScheduledOn: !this.scheduledOn,
        timeOfDay: this.timeOfDay || null,
        clearTimeOfDay: !this.timeOfDay,
        approxDurationMinutes: this.approxDurationMinutes,
        statusId: this.statusId,
        parentId: this.parentId || null,
        clearParent: !this.parentId,
        tags: this.selectedTags
      }).subscribe({
        next: () => {
          this.notification.showSuccess('Task updated.');
          this.goBack();
        },
        error: (err) => {
          this.isSaving = false;
          this.notification.showError(err?.error?.message || 'Could not save the task.');
        }
      });
      return;
    }

    this.tasks.createTask({
      title: this.title.trim(),
      notes: this.notes.trim() || null,
      category: this.category,
      priority: this.priority,
      scheduledOn: this.scheduledOn || null,
      timeOfDay: this.timeOfDay || null,
      approxDurationMinutes: this.approxDurationMinutes,
      statusId: this.statusId,
      parentId: this.parentId || null,
      tags: this.selectedTags
    }).subscribe({
      next: () => {
        this.notification.showSuccess('Task added.');
        this.goBack();
      },
      error: (err) => {
        this.isSaving = false;
        this.notification.showError(err?.error?.message || 'Could not add the task.');
      }
    });
  }

  async delete(): Promise<void> {
    if (!this.taskId) return;

    const extra = this.subtasks.length > 0
      ? ` Its ${this.subtasks.length} subtask(s) will be deleted too.`
      : '';
    const ok = await this.confirmDialog.confirm({
      title: 'Delete task',
      message: `Delete "${this.title}"?${extra} This cannot be undone.`,
      confirmLabel: 'Delete',
      danger: true
    });
    if (!ok) return;

    this.tasks.deleteTask(this.taskId).subscribe({
      next: () => {
        this.notification.showSuccess('Task deleted.');
        this.goBack();
      },
      error: () => this.notification.showError('Could not delete this task.')
    });
  }

  cancel(): void {
    this.goBack();
  }

  /** Returns to the view the form was opened from, restoring the calendar's month. */
  private goBack(): void {
    if (this.returnView === 'board') {
      this.router.navigate(['/dtasks/board']);
      return;
    }
    if (this.returnView === 'list') {
      this.router.navigate(['/dtasks/list']);
      return;
    }

    const queryParams = this.returnYear && this.returnMonth
      ? { year: this.returnYear, month: this.returnMonth }
      : {};
    this.router.navigate(['/dtasks'], { queryParams });
  }
}
