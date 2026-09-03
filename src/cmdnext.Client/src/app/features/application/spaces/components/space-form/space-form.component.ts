import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { SpacesService, SpaceTemplate } from '@features/application/spaces/services/spaces.service';
import { NotificationService } from '@core/services/notification.service';

@Component({
  selector: 'app-space-form',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './space-form.component.html',
  styleUrls: ['./space-form.component.scss']
})
export class SpaceFormComponent implements OnInit {
  templates: SpaceTemplate[] = [];
  selectedKind = 'custom';

  name = '';
  description = '';
  conventions = '';
  isSaving = false;

  constructor(
    private spacesService: SpacesService,
    private notification: NotificationService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.spacesService.getTemplates().subscribe({
      next: (t) => (this.templates = t),
      error: () => this.notification.showError('Could not load space templates.')
    });
  }

  get selectedTemplate(): SpaceTemplate | undefined {
    return this.templates.find(t => t.kind === this.selectedKind);
  }

  selectKind(kind: string): void {
    this.selectedKind = kind;
  }

  save(): void {
    const name = this.name.trim();
    if (!name) {
      this.notification.showError('Give the space a name.');
      return;
    }

    this.isSaving = true;
    this.spacesService.createSpace({
      name,
      kind: this.selectedKind,
      description: this.description.trim() || null,
      conventions: this.conventions.trim() || null
    }).subscribe({
      next: (space) => {
        this.isSaving = false;
        this.notification.showSuccess(`Space "${space.name}" created.`);
        this.router.navigate(['/spaces', space.id]);
      },
      error: () => {
        this.isSaving = false;
        this.notification.showError('Could not create the space.');
      }
    });
  }

  cancel(): void {
    this.router.navigate(['/spaces']);
  }
}
