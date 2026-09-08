import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';

export type TaskCategory = 'Task' | 'Subtask';
export type TaskPriority = 'Low' | 'Medium' | 'High';
export type TaskTimeOfDay = 'Morning' | 'Afternoon' | 'Evening' | 'Night';

export const PRIORITIES: TaskPriority[] = ['Low', 'Medium', 'High'];
export const CATEGORIES: TaskCategory[] = ['Task', 'Subtask'];
export const TIMES_OF_DAY: TaskTimeOfDay[] = ['Morning', 'Afternoon', 'Evening', 'Night'];

export interface TaskStatus {
  id: string;
  name: string;
  sortOrder: number;
  color?: string | null;
  isDone: boolean;
  isProtected: boolean;
  taskCount: number;
}

export interface CreateTaskStatusRequest {
  name: string;
  color?: string | null;
  isDone: boolean;
}

export interface UpdateTaskStatusRequest {
  name?: string | null;
  color?: string | null;
  isDone?: boolean | null;
  sortOrder?: number | null;
}

export interface TaskTag {
  id: string;
  name: string;
  color?: string | null;
  taskCount: number;
}

export interface CreateTaskTagRequest {
  name: string;
  color?: string | null;
}

export interface UpdateTaskTagRequest {
  name?: string | null;
  color?: string | null;
}

export interface Subtask {
  id: string;
  title: string;
  statusId: string;
  statusName: string;
  statusColor?: string | null;
  isDone: boolean;
  priority: TaskPriority;
  scheduledOn?: string | null;
  sortOrder: number;
}

export interface Task {
  id: string;
  title: string;
  notes?: string | null;
  category: TaskCategory;
  priority: TaskPriority;
  scheduledOn?: string | null;
  timeOfDay?: TaskTimeOfDay | null;
  approxDurationMinutes?: number | null;
  statusId: string;
  statusName: string;
  statusColor?: string | null;
  isDone: boolean;
  parentId?: string | null;
  parentTitle?: string | null;
  tags: string[];
  completedOn?: string | null;
  sortOrder: number;
  source: string;
  createdOn?: string | null;
  subtasks: Subtask[];
}

/** Row shape for the list / board / calendar views. */
export interface TaskListItem {
  id: string;
  title: string;
  category: TaskCategory;
  priority: TaskPriority;
  scheduledOn?: string | null;
  timeOfDay?: TaskTimeOfDay | null;
  approxDurationMinutes?: number | null;
  statusId: string;
  statusName: string;
  statusColor?: string | null;
  isDone: boolean;
  parentId?: string | null;
  tags: string[];
  hasNotes: boolean;
  subtaskCount: number;
  subtaskDoneCount: number;
  source: string;
  completedOn?: string | null;
  sortOrder: number;
}

export interface TaskBoardColumn {
  statusId: string;
  statusName: string;
  statusColor?: string | null;
  isDone: boolean;
  sortOrder: number;
  tasks: TaskListItem[];
}

export interface TaskPeriod {
  year: number;
  month: number;
  taskCount: number;
}

export interface DTaskDashboard {
  overdueCount: number;
  todayCount: number;
  todayDoneCount: number;
  upcomingCount: number;
  unscheduledCount: number;
  today: TaskListItem[];
  overdue: TaskListItem[];
}

export interface CreateTaskRequest {
  title: string;
  notes?: string | null;
  category?: TaskCategory | null;
  priority?: TaskPriority | null;
  scheduledOn?: string | null;
  timeOfDay?: TaskTimeOfDay | null;
  approxDurationMinutes?: number | null;
  statusId?: string | null;
  parentId?: string | null;
  tags: string[];
}

export interface UpdateTaskRequest {
  title?: string | null;
  notes?: string | null;
  category?: TaskCategory | null;
  priority?: TaskPriority | null;
  scheduledOn?: string | null;
  clearScheduledOn?: boolean;
  timeOfDay?: TaskTimeOfDay | null;
  clearTimeOfDay?: boolean;
  approxDurationMinutes?: number | null;
  statusId?: string | null;
  parentId?: string | null;
  clearParent?: boolean;
  tags?: string[] | null;
  sortOrder?: number | null;
}

export interface MoveTaskRequest {
  statusId?: string | null;
  scheduledOn?: string | null;
  clearScheduledOn?: boolean;
  sortOrder?: number | null;
}

/** Filters shared by the list and board views. */
export interface TaskFilters {
  statusIds?: string[];
  tags?: string[];
  priority?: TaskPriority | '';
  category?: TaskCategory | '';
  from?: string;
  to?: string;
  unscheduled?: boolean;
  includeDone?: boolean;
  topLevelOnly?: boolean;
  search?: string;
  sortBy?: string;
  sortDir?: 'asc' | 'desc';
  take?: number;
}

@Injectable({ providedIn: 'root' })
export class DTaskService {
  private readonly baseUrl = `${environment.apiUrl}/DTasks`;

  constructor(private http: HttpClient) {}

  // ---- Statuses ----
  getStatuses(): Observable<TaskStatus[]> {
    return this.http.get<TaskStatus[]>(`${this.baseUrl}/statuses`);
  }

  createStatus(request: CreateTaskStatusRequest): Observable<TaskStatus> {
    return this.http.post<TaskStatus>(`${this.baseUrl}/statuses`, request);
  }

  updateStatus(id: string, request: UpdateTaskStatusRequest): Observable<TaskStatus> {
    return this.http.put<TaskStatus>(`${this.baseUrl}/statuses/${id}`, request);
  }

  deleteStatus(id: string): Observable<unknown> {
    return this.http.delete(`${this.baseUrl}/statuses/${id}`);
  }

  reorderStatuses(statusIds: string[]): Observable<unknown> {
    return this.http.put(`${this.baseUrl}/statuses/reorder`, { statusIds });
  }

  // ---- Tags ----
  getTags(): Observable<TaskTag[]> {
    return this.http.get<TaskTag[]>(`${this.baseUrl}/tags`);
  }

  createTag(request: CreateTaskTagRequest): Observable<TaskTag> {
    return this.http.post<TaskTag>(`${this.baseUrl}/tags`, request);
  }

  updateTag(id: string, request: UpdateTaskTagRequest): Observable<TaskTag> {
    return this.http.put<TaskTag>(`${this.baseUrl}/tags/${id}`, request);
  }

  deleteTag(id: string): Observable<unknown> {
    return this.http.delete(`${this.baseUrl}/tags/${id}`);
  }

  // ---- Tasks ----
  getTasks(filters: TaskFilters = {}): Observable<TaskListItem[]> {
    return this.http.get<TaskListItem[]>(`${this.baseUrl}?${this.buildQuery(filters)}`);
  }

  getCalendar(year: number, month: number, includeDone = true): Observable<TaskListItem[]> {
    return this.http.get<TaskListItem[]>(
      `${this.baseUrl}/calendar?year=${year}&month=${month}&includeDone=${includeDone}`
    );
  }

  getBoard(filters: TaskFilters = {}): Observable<TaskBoardColumn[]> {
    return this.http.get<TaskBoardColumn[]>(`${this.baseUrl}/board?${this.buildQuery(filters)}`);
  }

  getPeriods(): Observable<TaskPeriod[]> {
    return this.http.get<TaskPeriod[]>(`${this.baseUrl}/periods`);
  }

  getDashboard(): Observable<DTaskDashboard> {
    return this.http.get<DTaskDashboard>(`${this.baseUrl}/dashboard`);
  }

  getTask(id: string): Observable<Task> {
    return this.http.get<Task>(`${this.baseUrl}/${id}`);
  }

  createTask(request: CreateTaskRequest): Observable<Task> {
    return this.http.post<Task>(this.baseUrl, request);
  }

  updateTask(id: string, request: UpdateTaskRequest): Observable<Task> {
    return this.http.put<Task>(`${this.baseUrl}/${id}`, request);
  }

  moveTask(id: string, request: MoveTaskRequest): Observable<Task> {
    return this.http.put<Task>(`${this.baseUrl}/${id}/move`, request);
  }

  toggleDone(id: string): Observable<Task> {
    return this.http.put<Task>(`${this.baseUrl}/${id}/toggle`, {});
  }

  deleteTask(id: string): Observable<unknown> {
    return this.http.delete(`${this.baseUrl}/${id}`);
  }

  /** Serialises filters, repeating statusId/tag for multi-select. */
  private buildQuery(f: TaskFilters): string {
    const params: string[] = [];
    for (const id of f.statusIds ?? []) params.push(`statusId=${encodeURIComponent(id)}`);
    for (const tag of f.tags ?? []) params.push(`tag=${encodeURIComponent(tag)}`);
    if (f.priority) params.push(`priority=${f.priority}`);
    if (f.category) params.push(`category=${f.category}`);
    if (f.from) params.push(`from=${f.from}`);
    if (f.to) params.push(`to=${f.to}`);
    if (f.unscheduled !== undefined) params.push(`unscheduled=${f.unscheduled}`);
    if (f.includeDone !== undefined) params.push(`includeDone=${f.includeDone}`);
    if (f.topLevelOnly !== undefined) params.push(`topLevelOnly=${f.topLevelOnly}`);
    if (f.search) params.push(`search=${encodeURIComponent(f.search)}`);
    if (f.sortBy) params.push(`sortBy=${f.sortBy}`);
    if (f.sortDir) params.push(`sortDir=${f.sortDir}`);
    if (f.take) params.push(`take=${f.take}`);
    return params.join('&');
  }
}
