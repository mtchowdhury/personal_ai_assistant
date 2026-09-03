import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { SpacesService, Space, EntryTypeSchema } from '@features/application/spaces/services/spaces.service';
import { NotificationService } from '@core/services/notification.service';
import { LoadingSpinnerComponent } from '@core/components/loading-spinner/loading-spinner.component';

@Component({
  selector: 'app-space-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, LoadingSpinnerComponent],
  templateUrl: './space-settings.component.html',
  styleUrls: ['./space-settings.component.scss']
})
export class SpaceSettingsComponent implements OnInit {
  spaceId = '';
  space: Space | null = null;
  isLoading = true;
  isSaving = false;

  name = '';
  description = '';
  conventions = '';
  entryTypes: EntryTypeSchema[] = [];

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private spacesService: SpacesService,
    private notification: NotificationService
  ) {}

  ngOnInit(): void {
    this.spaceId = this.route.snapshot.paramMap.get('id') ?? '';
    this.spacesService.getSpace(this.spaceId).subscribe({
      next: (space) => {
        this.space = space;
        this.name = space.name;
        this.description = space.description ?? '';
        this.conventions = space.conventions ?? '';
        this.entryTypes = this.parseSchema(space.schemaJson);
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.notification.showError('Could not load this space.');
      }
    });
  }

  save(): void {
    const name = this.name.trim();
    if (!name) {
      this.notification.showError('Give the space a name.');
      return;
    }

    this.isSaving = true;
    this.spacesService.updateSpace(this.spaceId, {
      name,
      description: this.description.trim() || null,
      conventions: this.conventions.trim() || null
    }).subscribe({
      next: () => {
        this.isSaving = false;
        this.notification.showSuccess('Settings saved.');
      },
      error: () => {
        this.isSaving = false;
        this.notification.showError('Could not save settings.');
      }
    });
  }

  archive(): void {
    this.spacesService.archiveSpace(this.spaceId).subscribe({
      next: () => {
        this.notification.showSuccess('Space archived.');
        this.router.navigate(['/spaces']);
      },
      error: () => this.notification.showError('Could not archive the space.')
    });
  }

  fieldNames(t: EntryTypeSchema): string {
    return t.fields.map(f => f.name).join(', ');
  }

  private parseSchema(json: string): EntryTypeSchema[] {
    try {
      return JSON.parse(json || '[]');
    } catch {
      return [];
    }
  }

  cancel(): void {
    this.router.navigate(['/spaces', this.spaceId]);
  }
}
