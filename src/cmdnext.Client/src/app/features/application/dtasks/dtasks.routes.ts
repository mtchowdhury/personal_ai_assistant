import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./components/dtasks-calendar/dtasks-calendar.component')
        .then(m => m.DTasksCalendarComponent),
    data: { title: 'Task Manager' }
  },
  {
    path: 'board',
    loadComponent: () =>
      import('./components/dtasks-board/dtasks-board.component')
        .then(m => m.DTasksBoardComponent),
    data: { title: 'Board' }
  },
  {
    path: 'list',
    loadComponent: () =>
      import('./components/dtasks-list/dtasks-list.component')
        .then(m => m.DTasksListComponent),
    data: { title: 'All Tasks' }
  },
  {
    path: 'new',
    loadComponent: () =>
      import('./components/task-form/task-form.component')
        .then(m => m.TaskFormComponent),
    data: { title: 'Add Task' }
  },
  {
    path: 'statuses',
    loadComponent: () =>
      import('./components/status-manager/status-manager.component')
        .then(m => m.StatusManagerComponent),
    data: { title: 'Statuses' }
  },
  {
    path: 'tags',
    loadComponent: () =>
      import('./components/tag-manager/tag-manager.component')
        .then(m => m.TagManagerComponent),
    data: { title: 'Tags' }
  },
  {
    path: ':id/edit',
    loadComponent: () =>
      import('./components/task-form/task-form.component')
        .then(m => m.TaskFormComponent),
    data: { title: 'Edit Task' }
  }
];
