import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import {
  DTaskService, TaskListItem, TaskStatus, TaskTag, TaskPriority, TaskCategory,
  PRIORITIES, CATEGORIES
} from '@features/application/dtasks/services/dtask.service';
import { NotificationService } from '@core/services/notification.service';
import { ConfirmDialogService } from '@core/services/confirm-dialog.service';
import { LoadingSpinnerComponent } from '@core/components/loading-spinner/loading-spinner.component';

/** The sortable columns, in the order they appear in the table head. */
const SORT_COLUMNS = ['title', 'scheduledOn', 'status', 'priority', 'duration', 'createdOn'] as const;
type SortColumn = typeof SORT_COLUMNS[number];

@Component({
  selector: 'app-dtasks-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LoadingSpinnerComponent],
  templateUrl: './dtasks-list.component.html',
  styleUrls: ['./dtasks-list.component.scss']
})
export class DTasksListComponent implements OnInit, OnDestroy {
  readonly priorities = PRIORITIES;
  readonly categories = CATEGORIES;

  tasks: TaskListItem[] = [];
  statuses: TaskStatus[] = [];
  tags: TaskTag[] = [];

  // Filters
  activeStatusIds: string[] = [];
  activeTags: string[] = [];
  priority: TaskPriority | '' = '';
  category: TaskCategory | '' = '';
  from = '';
  to = '';
  /** '' = any, 'yes' = only unscheduled, 'no' = only scheduled. */
  scheduled: '' | 'yes' | 'no' = '';
  includeDone = true;
  topLevelOnly = false;
  searchTerm = '';

  // Sorting
  sortBy: SortColumn = 'scheduledOn';
  sortDir: 'asc' | 'desc' = 'asc';

  isLoading = true;

  private readonly searchInput$ = new Subject<string>();
  private readonly destroy$ = new Subject<void>();

  constructor(
    private tasksApi: DTaskService,
    private notification: NotificationService,
    private confirmDialog: ConfirmDialogService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.tasksApi.getStatuses().subscribe({
      next: (s) => (this.statuses = s),
      error: () => {}
    });
    this.tasksApi.getTags().subscribe({
      next: (t) => (this.tags = t),
      error: () => {}
    });

    this.searchInput$
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => this.load());

    this.load();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  load(): void {
    this.isLoading = true;
    this.tasksApi.getTasks({
      statusIds: this.activeStatusIds,
      tags: this.activeTags,
      priority: this.priority || undefined,
      category: this.category || undefined,
      from: this.from || undefined,
      to: this.to || undefined,
      unscheduled: this.scheduled === '' ? undefined : this.scheduled === 'yes',
      includeDone: this.includeDone,
      topLevelOnly: this.topLevelOnly,
      search: this.searchTerm.trim() || undefined,
      sortBy: this.sortBy,
      sortDir: this.sortDir,
      take: 500
    }).subscribe({
      next: (list) => {
        this.tasks = list;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.notification.showError('Could not load tasks.');
      }
    });
  }

  onSearchChange(): void {
    this.searchInput$.next(this.searchTerm);
  }

  clearSearch(): void {
    this.searchTerm = '';
    this.load();
  }

  /** Clicking a column head sorts by it, or flips the direction if already active. */
  sort(column: SortColumn): void {
    if (this.sortBy === column) {
      this.sortDir = this.sortDir === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortBy = column;
      this.sortDir = 'asc';
    }
    this.load();
  }

  sortIcon(column: SortColumn): string {
    if (this.sortBy !== column) return '';
    return this.sortDir === 'asc' ? '▲' : '▼';
  }

  isStatusActive(id: string): boolean {
    return this.activeStatusIds.includes(id);
  }

  toggleStatus(id: string): void {
    this.activeStatusIds = this.isStatusActive(id)
      ? this.activeStatusIds.filter(s => s !== id)
      : [...this.activeStatusIds, id];
    this.load();
  }

  clearStatuses(): void {
    if (this.activeStatusIds.length === 0) return;
    this.activeStatusIds = [];
    this.load();
  }

  isTagActive(name: string): boolean {
    return this.activeTags.includes(name);
  }

  toggleTag(name: string): void {
    this.activeTags = this.isTagActive(name)
      ? this.activeTags.filter(t => t !== name)
      : [...this.activeTags, name];
    this.load();
  }

  clearTags(): void {
    if (this.activeTags.length === 0) return;
    this.activeTags = [];
    this.load();
  }

  /** True when anything other than the defaults is set, so the reset button can show. */
  get hasFilters(): boolean {
    return this.activeStatusIds.length > 0
      || this.activeTags.length > 0
      || !!this.priority
      || !!this.category
      || !!this.from
      || !!this.to
      || this.scheduled !== ''
      || !this.includeDone
      || this.topLevelOnly
      || !!this.searchTerm.trim();
  }

  resetFilters(): void {
    this.activeStatusIds = [];
    this.activeTags = [];
    this.priority = '';
    this.category = '';
    this.from = '';
    this.to = '';
    this.scheduled = '';
    this.includeDone = true;
    this.topLevelOnly = false;
    this.searchTerm = '';
    this.load();
  }

  get doneCount(): number {
    return this.tasks.filter(t => t.isDone).length;
  }

  /** Tasks open straight into the edit form; there is no separate detail view. */
  open(task: TaskListItem): void {
    this.router.navigate(['/dtasks', task.id, 'edit'], { queryParams: { from: 'list' } });
  }

  toggle(task: TaskListItem, event: Event): void {
    event.stopPropagation();
    this.tasksApi.toggleDone(task.id).subscribe({
      next: (updated) => {
        task.statusId = updated.statusId;
        task.statusName = updated.statusName;
        task.statusColor = updated.statusColor ?? null;
        task.isDone = updated.isDone;
        // The row may no longer belong in the current filter.
        if (!this.includeDone && updated.isDone) this.load();
      },
      error: () => this.notification.showError('Could not update the task.')
    });
  }

  async delete(task: TaskListItem, event: Event): Promise<void> {
    event.stopPropagation();

    const extra = task.subtaskCount > 0
      ? ` Its ${task.subtaskCount} subtask(s) will be deleted too.`
      : '';
    const ok = await this.confirmDialog.confirm({
      title: 'Delete task',
      message: `Delete "${task.title}"?${extra} This cannot be undone.`,
      confirmLabel: 'Delete',
      danger: true
    });
    if (!ok) return;

    this.tasksApi.deleteTask(task.id).subscribe({
      next: () => {
        this.notification.showSuccess('Task deleted.');
        this.load();
      },
      error: () => this.notification.showError('Could not delete this task.')
    });
  }

  goBack(): void {
    this.router.navigate(['/dashboard']);
  }
}
