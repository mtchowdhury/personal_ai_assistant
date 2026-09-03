import { Injectable, signal } from '@angular/core';

export type NotificationType = 'success' | 'error' | 'warning' | 'info';

export interface Notification {
  id: number;
  type: NotificationType;
  message: string;
  duration?: number;
}

@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  // Signal so consumers (the toast component's effect) react to every change.
  // The previous plain-array implementation never notified anyone, so no toast
  // was ever shown.
  private readonly _notifications = signal<Notification[]>([]);
  readonly notifications = this._notifications.asReadonly();

  private notificationId = 0;

  showSuccess(message: string, duration: number = 5000): void {
    this.show(message, 'success', duration);
  }

  showError(message: string, duration: number = 5000): void {
    this.show(message, 'error', duration);
  }

  showWarning(message: string, duration: number = 5000): void {
    this.show(message, 'warning', duration);
  }

  showInfo(message: string, duration: number = 5000): void {
    this.show(message, 'info', duration);
  }

  private show(message: string, type: NotificationType, duration: number): void {
    const notification: Notification = {
      id: ++this.notificationId,
      type,
      message,
      duration
    };
    this._notifications.update(list => [...list, notification]);

    if (duration > 0) {
      setTimeout(() => this.clear(notification.id), duration);
    }
  }

  clear(id?: number): void {
    if (id) {
      this._notifications.update(list => list.filter(n => n.id !== id));
    } else {
      this._notifications.set([]);
    }
  }
}
