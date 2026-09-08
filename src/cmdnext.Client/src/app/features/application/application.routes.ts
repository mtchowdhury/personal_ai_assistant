import { Routes } from '@angular/router';
import { authGuard } from '../../core/guards/auth.guard';

export const routes: Routes = [
  {
    path: '',
    redirectTo: 'dashboard',
    pathMatch: 'full'
  },
  {
    path: 'dashboard',
    loadChildren: () => import('./dashboard/dashboard.routes').then(m => m.routes),
    canActivate: [authGuard]
  },
  {
    path: 'ai-chat',
    loadChildren: () => import('./ai-chat/ai-chat.routes').then(m => m.routes),
    canActivate: [authGuard]
  },
  {
    path: 'finance',
    loadChildren: () => import('./finance/finance.routes').then(m => m.routes),
    canActivate: [authGuard]
  },
  {
    path: 'dtasks',
    loadChildren: () => import('./dtasks/dtasks.routes').then(m => m.routes),
    canActivate: [authGuard]
  },
  {
    path: 'spaces',
    loadChildren: () => import('./spaces/spaces.routes').then(m => m.routes),
    canActivate: [authGuard]
  }
];
