import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { SpacesService, Space, EntryTypeSchema, parseEntryTypeSchema } from '@features/application/spaces/services/spaces.service';
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
  isDeleting = false;

  showDeleteConfirm = false;
  deleteConfirmName = '';

  newTypeName = '';
  isSavingTypes = false;

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
        this.entryTypes = parseEntryTypeSchema(space.schemaJson);
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

  /** Hard delete is irreversible, so require the space's name to be typed back. */
  canDelete(): boolean {
    return !!this.space && this.deleteConfirmName.trim() === this.space.name;
  }

  cancelDelete(): void {
    this.showDeleteConfirm = false;
    this.deleteConfirmName = '';
  }

  deleteSpace(): void {
    if (!this.canDelete() || this.isDeleting) return;

    this.isDeleting = true;
    this.spacesService.deleteSpace(this.spaceId).subscribe({
      next: () => {
        this.isDeleting = false;
        this.notification.showSuccess('Space deleted.');
        this.router.navigate(['/spaces']);
      },
      error: () => {
        this.isDeleting = false;
        this.notification.showError('Could not delete the space.');
      }
    });
  }

  // ---- Entry types (per space; never shared with other spaces) ----

  addType(): void {
    const type = this.newTypeName.trim().toLowerCase();
    if (!type || this.isSavingTypes) return;

    if (this.entryTypes.some(t => t.type === type)) {
      this.notification.showError(`"${type}" is already an entry type here.`);
      return;
    }

    const next: EntryTypeSchema[] = [
      ...this.entryTypes,
      { type, label: type.charAt(0).toUpperCase() + type.slice(1), fields: [] }
    ];
    this.saveTypes(next, () => {
      this.newTypeName = '';
      this.notification.showSuccess(`Added "${type}".`);
    });
  }

  removeType(target: EntryTypeSchema): void {
    if (this.isSavingTypes) return;

    // Existing entries keep their type string, so removing one only takes it out of the
    // picker — it does not delete or rewrite anything already saved.
    const ok = confirm(
      `Remove the "${target.label}" type from this space?\n\n` +
      `Entries already saved as "${target.type}" keep their type and are not deleted, ` +
      `but you will no longer be able to pick it for new entries.`);
    if (!ok) return;

    const next = this.entryTypes.filter(t => t.type !== target.type);
    this.saveTypes(next, () => this.notification.showSuccess(`Removed "${target.label}".`));
  }

  private saveTypes(next: EntryTypeSchema[], done: () => void): void {
    this.isSavingTypes = true;
    this.spacesService.updateSpaceSchema(this.spaceId, JSON.stringify(next)).subscribe({
      next: (space) => {
        this.space = space;
        this.entryTypes = parseEntryTypeSchema(space.schemaJson);
        this.isSavingTypes = false;
        done();
      },
      error: () => {
        this.isSavingTypes = false;
        this.notification.showError('Could not update the entry types.');
      }
    });
  }

  fieldNames(t: EntryTypeSchema): string {
    return t.fields.map(f => f.name).join(', ');
  }

  cancel(): void {
    this.router.navigate(['/spaces', this.spaceId]);
  }
}
