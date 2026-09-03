import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { SpacesService, Space } from '@features/application/spaces/services/spaces.service';
import { NotificationService } from '@core/services/notification.service';
import { LoadingSpinnerComponent } from '@core/components/loading-spinner/loading-spinner.component';

const KIND_ICONS: Record<string, string> = {
  learning: '📚',
  journal: '📓',
  people: '🧑‍🤝‍🧑',
  process: '📋',
  custom: '🗂️'
};

@Component({
  selector: 'app-space-list',
  standalone: true,
  imports: [CommonModule, RouterLink, LoadingSpinnerComponent],
  templateUrl: './space-list.component.html',
  styleUrls: ['./space-list.component.scss']
})
export class SpaceListComponent implements OnInit {
  spaces: Space[] = [];
  isLoading = true;

  constructor(
    private spacesService: SpacesService,
    private notification: NotificationService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.isLoading = true;
    this.spacesService.getSpaces().subscribe({
      next: (spaces) => {
        this.spaces = spaces;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.notification.showError('Could not load spaces.');
      }
    });
  }

  icon(kind: string): string {
    return KIND_ICONS[kind] ?? KIND_ICONS['custom'];
  }

  stateLine(space: Space): string {
    try {
      const state = JSON.parse(space.stateJson || '{}');
      if (state.next) return `Next: ${state.next}`;
      if (state.lastCompleted) return `Last: ${state.lastCompleted}`;
    } catch {
      // ignore malformed state
    }
    return space.description || 'No progress recorded yet';
  }

  open(space: Space): void {
    this.router.navigate(['/spaces', space.id]);
  }

  goBack(): void {
    this.router.navigate(['/dashboard']);
  }
}
