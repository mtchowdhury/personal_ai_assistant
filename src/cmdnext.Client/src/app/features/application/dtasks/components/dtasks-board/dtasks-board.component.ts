import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import {
  DTaskService, TaskBoardColumn, TaskListItem, TaskTag, TaskPriority, PRIORITIES
} from '@features/application/dtasks/services/dtask.service';
import { NotificationService } from '@core/services/notification.service';
import { LoadingSpinnerComponent } from '@core/components/loading-spinner/loading-spinner.component';

@Component({
  selector: 'app-dtasks-board',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LoadingSpinnerComponent],
  templateUrl: './dtasks-board.component.html',
  styleUrls: ['./dtasks-board.component.scss']
})
export class DTasksBoardComponent implements OnInit, OnDestroy {
  readonly priorities = PRIORITIES;

  columns: TaskBoardColumn[] = [];
  tags: TaskTag[] = [];

  activeTags: string[] = [];
  priority: TaskPriority | '' = '';
  searchTerm = '';
  topLevelOnly = false;

  isLoading = true;

  private draggingId: string | null = null;
  dragOverStatusId: string | null = null;

  private readonly searchInput$ = new Subject<string>();
  private readonly destroy$ = new Subject<void>();

  constructor(
    private tasks: DTaskService,
    private notification: NotificationService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.tasks.getTags().subscribe({
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
    this.tasks.getBoard({
      tags: this.activeTags,
      priority: this.priority || undefined,
      topLevelOnly: this.topLevelOnly,
      search: this.searchTerm.trim() || undefined
    }).subscribe({
      next: (cols) => {
        this.columns = cols;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.notification.showError('Could not load the board.');
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

  get totalCount(): number {
    return this.columns.reduce((sum, c) => sum + c.tasks.length, 0);
  }

  /** Tasks open straight into the edit form; there is no separate detail view. */
  open(task: TaskListItem): void {
    this.router.navigate(['/dtasks', task.id, 'edit'], { queryParams: { from: 'board' } });
  }

  // ---- Drag and drop between columns ----

  onDragStart(task: TaskListItem, event: DragEvent): void {
    this.draggingId = task.id;
    event.dataTransfer?.setData('text/plain', task.id);
    if (event.dataTransfer) event.dataTransfer.effectAllowed = 'move';
  }

  onDragEnd(): void {
    this.draggingId = null;
    this.dragOverStatusId = null;
  }

  onDragOver(column: TaskBoardColumn, event: DragEvent): void {
    event.preventDefault();
    if (event.dataTransfer) event.dataTransfer.dropEffect = 'move';
    this.dragOverStatusId = column.statusId;
  }

  onDragLeave(column: TaskBoardColumn): void {
    if (this.dragOverStatusId === column.statusId) this.dragOverStatusId = null;
  }

  onDrop(column: TaskBoardColumn, event: DragEvent): void {
    event.preventDefault();
    this.dragOverStatusId = null;

    const id = this.draggingId ?? event.dataTransfer?.getData('text/plain');
    this.draggingId = null;
    if (!id) return;

    const source = this.columns.find(c => c.tasks.some(t => t.id === id));
    if (!source || source.statusId === column.statusId) return;

    // Move optimistically so the card lands under the cursor immediately, then
    // reconcile with the server's version of the task.
    const index = source.tasks.findIndex(t => t.id === id);
    const [task] = source.tasks.splice(index, 1);
    column.tasks = [...column.tasks, task];

    this.tasks.moveTask(id, { statusId: column.statusId }).subscribe({
      next: (updated) => {
        task.statusId = updated.statusId;
        task.statusName = updated.statusName;
        task.statusColor = updated.statusColor ?? null;
        task.isDone = updated.isDone;
      },
      error: () => {
        this.notification.showError('Could not move the task.');
        this.load();
      }
    });
  }

  goBack(): void {
    this.router.navigate(['/dashboard']);
  }
}
