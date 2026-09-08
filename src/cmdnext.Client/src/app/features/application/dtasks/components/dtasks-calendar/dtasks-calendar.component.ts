import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { DTaskService, TaskListItem } from '@features/application/dtasks/services/dtask.service';
import { NotificationService } from '@core/services/notification.service';
import { LoadingSpinnerComponent } from '@core/components/loading-spinner/loading-spinner.component';

const MONTH_NAMES = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December'
];

/** One cell of the month grid. */
interface CalendarDay {
  date: Date;
  /** ISO yyyy-MM-dd, used as the drop target key and for navigation. */
  key: string;
  dayOfMonth: number;
  inCurrentMonth: boolean;
  isToday: boolean;
  tasks: TaskListItem[];
}

@Component({
  selector: 'app-dtasks-calendar',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LoadingSpinnerComponent],
  templateUrl: './dtasks-calendar.component.html',
  styleUrls: ['./dtasks-calendar.component.scss']
})
export class DTasksCalendarComponent implements OnInit {
  readonly weekdays = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

  year = new Date().getFullYear();
  month = new Date().getMonth() + 1;

  weeks: CalendarDay[][] = [];
  includeDone = true;
  isLoading = true;

  /** Id of the task currently being dragged, so a drop knows what to move. */
  private draggingId: string | null = null;
  dragOverKey: string | null = null;

  constructor(
    private tasks: DTaskService,
    private notification: NotificationService,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    // Returning from the task form restores the month that was open.
    const query = this.route.snapshot.queryParamMap;
    const year = Number(query.get('year'));
    const month = Number(query.get('month'));
    if (year > 0 && month >= 1 && month <= 12) {
      this.year = year;
      this.month = month;
    }

    this.load();
  }

  get monthLabel(): string {
    return `${MONTH_NAMES[this.month - 1]} ${this.year}`;
  }

  load(): void {
    this.isLoading = true;
    this.tasks.getCalendar(this.year, this.month, this.includeDone).subscribe({
      next: (list) => {
        this.weeks = this.buildGrid(list);
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.weeks = this.buildGrid([]);
        this.notification.showError('Could not load the calendar.');
      }
    });
  }

  /**
   * Lays the month out as whole Sunday-start weeks, padding with the adjacent months'
   * days so every row has seven cells.
   */
  private buildGrid(list: TaskListItem[]): CalendarDay[][] {
    const byDate = new Map<string, TaskListItem[]>();
    for (const task of list) {
      if (!task.scheduledOn) continue;
      const key = task.scheduledOn.substring(0, 10);
      const bucket = byDate.get(key);
      if (bucket) bucket.push(task);
      else byDate.set(key, [task]);
    }

    const first = new Date(this.year, this.month - 1, 1);
    const start = new Date(first);
    start.setDate(1 - first.getDay());

    const todayKey = this.toKey(new Date());
    const weeks: CalendarDay[][] = [];

    for (let w = 0; w < 6; w++) {
      const week: CalendarDay[] = [];
      for (let d = 0; d < 7; d++) {
        const date = new Date(start);
        date.setDate(start.getDate() + w * 7 + d);
        const key = this.toKey(date);

        week.push({
          date,
          key,
          dayOfMonth: date.getDate(),
          inCurrentMonth: date.getMonth() === this.month - 1,
          isToday: key === todayKey,
          tasks: byDate.get(key) ?? []
        });
      }
      weeks.push(week);

      // A month never needs a sixth row unless it actually spills into one.
      const lastDay = week[6].date;
      if (lastDay.getMonth() !== this.month - 1 && lastDay > first) break;
    }

    return weeks;
  }

  /** Local-date ISO key; avoids the UTC shift toISOString() would introduce. */
  private toKey(date: Date): string {
    const m = `${date.getMonth() + 1}`.padStart(2, '0');
    const d = `${date.getDate()}`.padStart(2, '0');
    return `${date.getFullYear()}-${m}-${d}`;
  }

  prevMonth(): void {
    if (this.month === 1) { this.month = 12; this.year--; }
    else this.month--;
    this.load();
  }

  nextMonth(): void {
    if (this.month === 12) { this.month = 1; this.year++; }
    else this.month++;
    this.load();
  }

  goToday(): void {
    const now = new Date();
    this.year = now.getFullYear();
    this.month = now.getMonth() + 1;
    this.load();
  }

  onIncludeDoneChange(): void {
    this.load();
  }

  /** Tasks open straight into the edit form; there is no separate detail view. */
  open(task: TaskListItem): void {
    this.router.navigate(['/dtasks', task.id, 'edit'], { queryParams: this.returnParams });
  }

  /** Opens the form pre-dated to the clicked day. */
  addOn(day: CalendarDay): void {
    this.router.navigate(['/dtasks/new'], {
      queryParams: { date: day.key, ...this.returnParams }
    });
  }

  /** Tells the form which view and month to come back to. */
  private get returnParams(): { from: string; year: number; month: number } {
    return { from: 'calendar', year: this.year, month: this.month };
  }

  toggle(task: TaskListItem, event: Event): void {
    event.stopPropagation();
    this.tasks.toggleDone(task.id).subscribe({
      next: (updated) => {
        task.statusId = updated.statusId;
        task.statusName = updated.statusName;
        task.statusColor = updated.statusColor ?? null;
        task.isDone = updated.isDone;
        if (!this.includeDone && updated.isDone) this.load();
      },
      error: () => this.notification.showError('Could not update the task.')
    });
  }

  // ---- Drag and drop between days ----

  onDragStart(task: TaskListItem, event: DragEvent): void {
    this.draggingId = task.id;
    event.dataTransfer?.setData('text/plain', task.id);
    if (event.dataTransfer) event.dataTransfer.effectAllowed = 'move';
  }

  onDragEnd(): void {
    this.draggingId = null;
    this.dragOverKey = null;
  }

  onDragOver(day: CalendarDay, event: DragEvent): void {
    event.preventDefault();
    if (event.dataTransfer) event.dataTransfer.dropEffect = 'move';
    this.dragOverKey = day.key;
  }

  onDragLeave(day: CalendarDay): void {
    if (this.dragOverKey === day.key) this.dragOverKey = null;
  }

  onDrop(day: CalendarDay, event: DragEvent): void {
    event.preventDefault();
    this.dragOverKey = null;

    const id = this.draggingId ?? event.dataTransfer?.getData('text/plain');
    this.draggingId = null;
    if (!id) return;

    // No-op when the task is dropped back on the day it came from.
    const current = this.weeks.flat().find(d => d.tasks.some(t => t.id === id));
    if (current?.key === day.key) return;

    this.tasks.moveTask(id, { scheduledOn: day.key }).subscribe({
      next: () => this.load(),
      error: () => this.notification.showError('Could not move the task.')
    });
  }

  goBack(): void {
    this.router.navigate(['/dashboard']);
  }
}
