import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./components/space-list/space-list.component').then(m => m.SpaceListComponent),
    data: { title: 'Spaces' }
  },
  {
    path: 'new',
    loadComponent: () =>
      import('./components/space-form/space-form.component').then(m => m.SpaceFormComponent),
    data: { title: 'New Space' }
  },
  {
    path: 'search',
    loadComponent: () =>
      import('./components/space-search/space-search.component').then(m => m.SpaceSearchComponent),
    data: { title: 'Search' }
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./components/space-detail/space-detail.component').then(m => m.SpaceDetailComponent),
    data: { title: 'Space' }
  },
  {
    path: ':id/settings',
    loadComponent: () =>
      import('./components/space-settings/space-settings.component').then(m => m.SpaceSettingsComponent),
    data: { title: 'Space Settings' }
  },
  {
    path: ':id/entries/new',
    loadComponent: () =>
      import('./components/entry-detail/entry-detail.component').then(m => m.EntryDetailComponent),
    data: { title: 'New Entry' }
  },
  {
    path: ':id/entries/:entryId',
    loadComponent: () =>
      import('./components/entry-detail/entry-detail.component').then(m => m.EntryDetailComponent),
    data: { title: 'Entry' }
  }
];
