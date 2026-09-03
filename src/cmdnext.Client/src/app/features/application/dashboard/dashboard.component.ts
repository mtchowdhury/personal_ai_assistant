import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterOutlet } from '@angular/router';
import { AuthService } from 'src/app/core/services/auth.service';
import { AiChatService, AiChatSession } from '@features/application/ai-chat/services/ai-chat.service';
import { User } from 'src/app/core/services/auth.service';
import { LoadingSpinnerComponent } from '@core/components/loading-spinner/loading-spinner.component';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterOutlet, LoadingSpinnerComponent],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements OnInit {
  user: User | null = null;
  isLoading = true;

  totalChats = 0;

  recentItems: { type: string; title: string; date: Date; route: string }[] = [];

  constructor(
    private authService: AuthService,
    private aiChatService: AiChatService
  ) {}

  ngOnInit(): void {
    this.user = this.authService.getCurrentUser();
    this.loadData();
  }

  private loadData(): void {
    this.isLoading = true;
    this.aiChatService.getSessions().subscribe({
      next: (sessions) => {
        this.totalChats = sessions.length;
        this.recentItems = sessions
          .slice()
          .sort((a, b) => this.timestamp(b) - this.timestamp(a))
          .slice(0, 5)
          .map(s => ({
            type: 'chat',
            title: s.title || 'New chat',
            date: new Date(s.lastMessageAt || s.createdOn || Date.now()),
            route: '/ai-chat'
          }));
        this.isLoading = false;
      },
      error: () => {
        // Leave the counts at zero; the empty states cover this.
        this.isLoading = false;
      }
    });
  }

  private timestamp(session: AiChatSession): number {
    const value = session.lastMessageAt || session.createdOn;
    return value ? new Date(value).getTime() : 0;
  }

  logout(): void {
    this.authService.logout();
  }

  get userInitials(): string {
    if (!this.user) return 'C';
    const firstName = this.user.firstName || '';
    const lastName = this.user.lastName || '';
    return (firstName.charAt(0) + lastName.charAt(0)).toUpperCase();
  }
}
