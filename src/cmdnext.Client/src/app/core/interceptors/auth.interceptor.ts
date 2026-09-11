import { Injectable } from '@angular/core';
import {
  HttpRequest,
  HttpHandler,
  HttpEvent,
  HttpInterceptor,
  HttpErrorResponse
} from '@angular/common/http';
import { Observable, catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { Router } from '@angular/router';

@Injectable()
export class AuthInterceptor implements HttpInterceptor {
  constructor(private authService: AuthService, private router: Router) {}

  intercept(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    // The auth endpoints are how a session is obtained; they must not carry a stale token,
    // and a 401 from them means "wrong password", not "session over".
    if (req.url.includes('/auth/login') || req.url.includes('/auth/register')) {
      return next.handle(req);
    }

    const token = this.authService.getToken();

    // An expired token used to be sent as a bare request whose 401 nothing handled, so the
    // page sat spinning or showed a generic error. End the session up front instead: the
    // outcome is the same (the token cannot be renewed — there is no refresh flow) but the
    // user lands on the login page rather than on a stuck screen.
    if (token && this.authService.isTokenExpired()) {
      this.endSession();
      return throwError(() => new HttpErrorResponse({
        status: 401,
        statusText: 'Unauthorized',
        url: req.url,
        error: { message: 'Your session has expired. Please sign in again.' }
      }));
    }

    const authReq = token
      ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : req;

    // Attached in both cases: a token can also be rejected server-side (revoked, restarted
    // with a new signing key), and that 401 needs the same handling.
    return next.handle(authReq).pipe(
      catchError((error: HttpErrorResponse) => {
        if (error.status === 401) this.endSession();
        return throwError(() => error);
      })
    );
  }

  /** Clears the stored session and sends the user to login, once. */
  private endSession(): void {
    // Already on the login page (or heading there) — don't stack navigations.
    if (this.router.url.startsWith('/auth/')) {
      this.authService.logout(false);
      return;
    }

    this.authService.logout(false);
    // returnUrl so the user comes back to what they were looking at after signing in.
    this.router.navigate(['/auth/login'], {
      queryParams: { returnUrl: this.router.url }
    });
  }
}
