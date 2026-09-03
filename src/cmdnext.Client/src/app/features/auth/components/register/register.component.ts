import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthService } from '@core/services/auth.service';
import { NotificationService } from '@core/services/notification.service';
import { Router, RouterLink } from '@angular/router';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatCardModule,
    MatProgressSpinnerModule
  ],
  template: `
    <div class="register-container">
      <mat-card class="register-card">
        <mat-card-header>
          <mat-card-title>Register</mat-card-title>
        </mat-card-header>
        <mat-card-content>
          <form [formGroup]="registerForm" (ngSubmit)="onSubmit()">
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Email</mat-label>
              <input matInput formControlName="email" type="email" required autocomplete="email">
            </mat-form-field>
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>First Name</mat-label>
              <input matInput formControlName="firstName">
            </mat-form-field>
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Last Name</mat-label>
              <input matInput formControlName="lastName">
            </mat-form-field>
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Password</mat-label>
              <input matInput formControlName="password" type="password" required autocomplete="new-password">
              <mat-hint>At least 6 characters</mat-hint>
            </mat-form-field>
            @if (errorMessage) {
              <div class="error-banner" role="alert">{{ errorMessage }}</div>
            }
            <div class="button-group">
              <button mat-raised-button color="primary" type="submit" [disabled]="registerForm.invalid || isLoading">
                <span *ngIf="!isLoading">Register</span>
                <mat-progress-spinner *ngIf="isLoading" diameter="18" mode="indeterminate"></mat-progress-spinner>
              </button>
              <a routerLink="/auth/login" mat-button>Already have an account? Login</a>
            </div>
          </form>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .error-banner {
      background: #fdecea;
      color: #b71c1c;
      border: 1px solid #f5c6cb;
      border-radius: 4px;
      padding: 10px 12px;
      margin-bottom: 12px;
      font-size: 14px;
    }
    .register-container {
      display: flex;
      justify-content: center;
      align-items: center;
      min-height: 100vh;
      padding: 20px;
      background: #f5f5f5;
    }
    .register-card {
      width: 100%;
      max-width: 400px;
    }
    .full-width {
      width: 100%;
      margin-bottom: 16px;
    }
    .button-group {
      display: flex;
      gap: 16px;
      align-items: center;
      margin-top: 20px;
    }
    .button-group button[mat-raised-button] {
      display: flex;
      align-items: center;
      justify-content: center;
    }
    .button-group ::ng-deep mat-progress-spinner circle {
      stroke: #ffffff;
    }
  `]
})
export class RegisterComponent {
  registerForm: FormGroup;
  errorMessage: string | null = null;

  isLoading = false;

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private notification: NotificationService,
    private router: Router
  ) {
    this.registerForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      firstName: [''],
      lastName: [''],
      password: ['', [Validators.required, Validators.minLength(6)]]
    });
  }

  onSubmit(): void {
    this.errorMessage = null;
    if (this.registerForm.invalid || this.isLoading) {
      this.registerForm.markAllAsTouched();
      return;
    }

    this.isLoading = true;
    const { email, firstName, lastName, password } = this.registerForm.value;

    this.authService.register(email, password, firstName ?? '', lastName ?? '').subscribe({
      next: () => {
        this.isLoading = false;
        this.notification.showSuccess('Account created successfully!');
        // AuthService already stored the token and navigated home.
      },
      error: (err) => {
        this.isLoading = false;
        const message = err?.error?.message || 'Registration failed. Please try again.';
        this.errorMessage = message;
        this.notification.showError(message);
      }
    });
  }
}
