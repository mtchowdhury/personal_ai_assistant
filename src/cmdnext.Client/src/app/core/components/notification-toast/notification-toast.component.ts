import { Component, inject, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { NotificationService, Notification } from '../../services/notification.service';

@Component({
  selector: 'app-notification-toast',
  standalone: true,
  imports: [CommonModule, MatSnackBarModule],
  templateUrl: './notification-toast.component.html',
  styleUrls: ['./notification-toast.component.scss']
})
export class NotificationToastComponent {
  private notificationService = inject(NotificationService);
  private snackBar = inject(MatSnackBar);

  // Track which notifications have already been shown so a list change (e.g. a
  // toast being cleared) doesn't re-open an earlier one.
  private shownIds = new Set<number>();

  constructor() {
    // Reads the signal, so the effect re-runs whenever a notification is added.
    effect(() => {
      const notifications = this.notificationService.notifications();
      for (const notification of notifications) {
        if (!this.shownIds.has(notification.id)) {
          this.shownIds.add(notification.id);
          this.showToast(notification);
        }
      }
    });
  }

  private showToast(notification: Notification): void {
    const panelClass = `toast-${notification.type}`;

    this.snackBar.open(notification.message, 'Dismiss', {
      duration: notification.duration || 5000,
      panelClass: [panelClass, 'notification-toast'],
      horizontalPosition: 'end',
      verticalPosition: 'top'
    });
  }
}
