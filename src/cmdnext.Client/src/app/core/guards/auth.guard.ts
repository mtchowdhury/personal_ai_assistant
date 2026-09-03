import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';
import { map, catchError, of } from 'rxjs';

export const authGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isLoggedIn()) {
    return true;
  }

  // Try to refresh or check token
  const token = authService.getToken();
  if (!token) {
    return router.createUrlTree(['/auth/login']);
  }

  // If we have a token but isAuthenticated is false, something is wrong
  // For now, redirect to login
  return router.createUrlTree(['/auth/login']);
};
