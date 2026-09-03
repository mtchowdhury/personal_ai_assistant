import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { environment } from '@environments/environment';
import { Router } from '@angular/router';

export interface User {
  id: string;
  email: string;
  firstName?: string;
  lastName?: string;
}

export interface AuthResponse {
  token: string;
  user: User;
  expiresIn: number;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest extends LoginRequest {
  firstName: string;
  lastName: string;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly apiUrl = `${environment.apiUrl}/auth`;
  private readonly tokenKey = 'auth_token';
  private readonly userKey = 'auth_user';
  private readonly expiresKey = 'auth_expires';

  private _isAuthenticated = new BehaviorSubject<boolean>(false);
  isAuthenticated$ = this._isAuthenticated.asObservable();

  constructor(private http: HttpClient, private router: Router) {
    this.initialize();
  }

  private initialize(): void {
    const token = this.getToken();
    const user = this.getUser();
    const expires = this.getExpires();

    if (token && user && expires && new Date(expires) > new Date()) {
      this._isAuthenticated.next(true);
    } else {
      this.logout(false);
    }
  }

  getToken(): string | null {
    return localStorage.getItem(this.tokenKey);
  }

  getUser(): User | null {
    const user = localStorage.getItem(this.userKey);
    return user ? JSON.parse(user) : null;
  }

  private getExpires(): string | null {
    return localStorage.getItem(this.expiresKey);
  }

  getCurrentUser(): User | null {
    return this.getUser();
  }

  isLoggedIn(): boolean {
    return this._isAuthenticated.value;
  }

  isTokenExpired(): boolean {
    const expires = this.getExpires();
    if (!expires) return true;
    return new Date(expires) <= new Date();
  }

  login(email: string, password: string): Observable<AuthResponse> {
    const request: LoginRequest = { email, password };
    
    return this.http.post<AuthResponse>(`${this.apiUrl}/login`, request).pipe(
      tap(response => this.setAuthData(response))
    );
  }

  register(email: string, password: string, firstName: string, lastName: string): Observable<AuthResponse> {
    const request: RegisterRequest = { email, password, firstName, lastName };
    
    return this.http.post<AuthResponse>(`${this.apiUrl}/register`, request).pipe(
      tap(response => this.setAuthData(response))
    );
  }

  logout(redirect = true): void {
    localStorage.removeItem(this.tokenKey);
    localStorage.removeItem(this.userKey);
    localStorage.removeItem(this.expiresKey);
    this._isAuthenticated.next(false);

    if (redirect) {
      this.router.navigate(['/auth/login']);
    }
  }

  private setAuthData(response: AuthResponse): void {
    const expiresAt = new Date(Date.now() + response.expiresIn * 1000).toISOString();
    
    localStorage.setItem(this.tokenKey, response.token);
    localStorage.setItem(this.userKey, JSON.stringify(response.user));
    localStorage.setItem(this.expiresKey, expiresAt);
    
    this._isAuthenticated.next(true);
    this.router.navigate(['/']);
  }
}
