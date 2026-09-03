import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';
import { NotificationToastComponent } from './core/components/notification-toast/notification-toast.component';
import { MatSnackBarModule } from '@angular/material/snack-bar';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, NotificationToastComponent, MatSnackBarModule],
  template: `
    <router-outlet></router-outlet>
    <app-notification-toast></app-notification-toast>
  `
})
export class AppComponent {
  title = 'CmdNext';
}
