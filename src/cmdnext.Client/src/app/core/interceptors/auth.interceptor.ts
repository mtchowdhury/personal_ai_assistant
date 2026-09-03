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
    const token = this.authService.getToken();
    const isLoggedIn = this.authService.isLoggedIn();
    const isExpired = this.authService.isTokenExpired();

    // Only add token if user is logged in, token exists, and it's not expired
    if (token && isLoggedIn && !isExpired) {
      const authReq = req.clone({
        setHeaders: {
          Authorization: `Bearer ${token}`
        }
      });
      return next.handle(authReq).pipe(
        catchError((error: HttpErrorResponse) => {
          if (error.status === 401) {
            // Token is invalid - force logout and redirect
            this.authService.logout(false);
            this.router.navigate(['/auth/login']);
          }
          return throwError(() => error);
        })
      );
    }

    // No token or expired - send request as-is
    // The API will return 401 which will be handled by the component
    return next.handle(req);
  }
}
