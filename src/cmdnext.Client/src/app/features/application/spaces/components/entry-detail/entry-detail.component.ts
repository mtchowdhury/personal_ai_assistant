import { AfterViewChecked, Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  SpacesService, Space, Entry, EntryTypeSchema, FieldSchema, Attachment, parseEntryTypeSchema
} from '@features/application/spaces/services/spaces.service';
import { NotificationService } from '@core/services/notification.service';
import { LoadingSpinnerComponent } from '@core/components/loading-spinner/loading-spinner.component';
import { ChatMarkdownPipe } from '@shared/pipes/chat-markdown.pipe';

@Component({
  selector: 'app-entry-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LoadingSpinnerComponent, ChatMarkdownPipe],
  templateUrl: './entry-detail.component.html',
  styleUrls: ['./entry-detail.component.scss']
})
export class EntryDetailComponent implements OnInit, AfterViewChecked {
  @ViewChild('bodyTextarea') bodyTextarea?: ElementRef<HTMLTextAreaElement>;
  private lastGrownFor: HTMLTextAreaElement | null = null;

  spaceId = '';
  entryId: string | null = null;
  isNew = false;
  isLoading = true;
  isSaving = false;

  /** New entries start in edit mode (nothing to render yet); existing ones open rendered. */
  isEditingBody = false;

  space: Space | null = null;
  entryTypes: EntryTypeSchema[] = [];

  type = 'note';
  title = '';
  body = '';
  tagsText = '';
  occurredOn = '';
  dueOn = '';
  status = '';
  fieldValues: Record<string, string> = {};

  createdOn: string | null = null;
  updatedOn: string | null = null;
  source = 'manual';

  attachments: Attachment[] = [];

  appendText = '';
  showAppend = false;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private spacesService: SpacesService,
    private notification: NotificationService
  ) {}

  ngOnInit(): void {
    this.spaceId = this.route.snapshot.paramMap.get('id') ?? '';
    this.entryId = this.route.snapshot.paramMap.get('entryId');
    this.isNew = !this.entryId;

    this.spacesService.getSpace(this.spaceId).subscribe({
      next: (space) => {
        this.space = space;
        this.entryTypes = parseEntryTypeSchema(space.schemaJson);
        if (this.entryTypes.length && !this.entryTypes.some(t => t.type === this.type)) {
          this.type = this.entryTypes[0].type;
        }
        this.onTypeChange();

        if (this.isNew) {
          this.isEditingBody = true;
          this.isLoading = false;
        } else {
          this.loadEntry();
        }
      },
      error: () => {
        this.isLoading = false;
        this.notification.showError('Could not load this space.');
      }
    });
  }

  private loadEntry(): void {
    this.spacesService.getEntry(this.spaceId, this.entryId!).subscribe({
      next: (entry) => {
        this.applyEntry(entry);
        this.isEditingBody = false;
        this.isLoading = false;
        this.loadAttachments();
      },
      error: () => {
        this.isLoading = false;
        this.notification.showError('Could not load this entry.');
      }
    });
  }

  private applyEntry(entry: Entry): void {
    this.type = entry.type;
    // The type may have since been removed from the space's schema. Keep it selectable so the
    // dropdown does not render blank and silently rewrite the entry's type on the next save.
    if (this.type && !this.entryTypes.some(t => t.type === this.type)) {
      this.entryTypes = [...this.entryTypes, { type: this.type, label: this.type, fields: [] }];
    }
    this.title = entry.title;
    this.body = entry.body;
    this.tagsText = entry.tags.join(', ');
    this.occurredOn = entry.occurredOn ? entry.occurredOn.substring(0, 10) : '';
    this.dueOn = entry.dueOn ? entry.dueOn.substring(0, 10) : '';
    this.status = entry.status ?? '';
    this.createdOn = entry.createdOn ?? null;
    this.updatedOn = entry.updatedOn ?? null;
    this.source = entry.source;
    this.onTypeChange(entry.fieldsJson);
  }

  private loadAttachments(): void {
    if (!this.entryId) return;
    this.spacesService.getAttachments(this.spaceId, undefined, this.entryId).subscribe({
      next: (list) => (this.attachments = list),
      error: () => {}
    });
  }

  toggleEditBody(): void {
    this.isEditingBody = !this.isEditingBody;
    this.lastGrownFor = null;
  }

  autoGrow(event: Event): void {
    const el = event.target as HTMLTextAreaElement;
    el.style.height = 'auto';
    el.style.height = `${el.scrollHeight}px`;
  }

  ngAfterViewChecked(): void {
    // The textarea only exists in the DOM once isEditingBody is true, and its content can
    // change (loading an existing entry, toggling into edit) without an (input) event firing —
    // grow it to fit whenever a new element instance appears.
    const el = this.bodyTextarea?.nativeElement;
    if (el && el !== this.lastGrownFor) {
      el.style.height = 'auto';
      el.style.height = `${el.scrollHeight}px`;
      this.lastGrownFor = el;
    }
  }

  currentTypeSchema(): EntryTypeSchema | undefined {
    return this.entryTypes.find(t => t.type === this.normalizedType());
  }

  /** Types are stored lowercase/trimmed so "Recipe" and "recipe" cannot both exist. */
  private normalizedType(): string {
    return this.type.trim().toLowerCase();
  }



  currentFields(): FieldSchema[] {
    return this.currentTypeSchema()?.fields ?? [];
  }

  onTypeChange(fieldsJson?: string): void {
    const fields = this.currentFields();
    const parsed = fieldsJson ? this.parseFields(fieldsJson) : {};
    const next: Record<string, string> = {};
    for (const f of fields) {
      next[f.name] = parsed[f.name] ?? this.fieldValues[f.name] ?? '';
    }
    this.fieldValues = next;
  }

  save(): void {
    const title = this.title.trim();
    if (!title) {
      this.notification.showError('Give the entry a title.');
      return;
    }

    const type = this.normalizedType();
    if (!type) {
      this.notification.showError('Give the entry a type.');
      return;
    }

    const tags = this.tagsText.split(',').map(t => t.trim()).filter(t => t.length > 0);
    const fieldsJson = JSON.stringify(this.fieldValues);

    this.isSaving = true;

    this.persist(type, title, tags, fieldsJson);
  }

  private persist(type: string, title: string, tags: string[], fieldsJson: string): void {
    if (this.isNew) {
      const nodeId = this.route.snapshot.queryParamMap.get('nodeId');
      this.spacesService.createEntry(this.spaceId, {
        nodeId: nodeId || null,
        type,
        title,
        body: this.body,
        fieldsJson,
        tags,
        occurredOn: this.occurredOn || null,
        dueOn: this.dueOn || null,
        status: this.status || null
      }).subscribe({
        next: (entry) => {
          this.isSaving = false;
          this.notification.showSuccess('Entry created.');
          this.router.navigate(['/spaces', this.spaceId, 'entries', entry.id]);
        },
        error: () => {
          this.isSaving = false;
          this.notification.showError('Could not create the entry.');
        }
      });
    } else {
      this.spacesService.updateEntry(this.spaceId, this.entryId!, {
        title,
        body: this.body,
        fieldsJson,
        tags,
        occurredOn: this.occurredOn || null,
        dueOn: this.dueOn || null,
        status: this.status || null
      }).subscribe({
        next: (entry) => {
          this.isSaving = false;
          this.applyEntry(entry);
          this.isEditingBody = false;
          this.notification.showSuccess('Entry saved.');
        },
        error: () => {
          this.isSaving = false;
          this.notification.showError('Could not save the entry.');
        }
      });
    }
  }

  submitAppend(): void {
    const text = this.appendText.trim();
    if (!text || !this.entryId) return;

    this.spacesService.appendToEntry(this.spaceId, this.entryId, { text }).subscribe({
      next: (entry) => {
        this.applyEntry(entry);
        this.appendText = '';
        this.showAppend = false;
        this.notification.showSuccess('Appended.');
      },
      error: () => this.notification.showError('Could not append to the entry.')
    });
  }

  deleteEntry(): void {
    if (!this.entryId) return;
    this.spacesService.deleteEntry(this.spaceId, this.entryId).subscribe({
      next: () => {
        this.notification.showSuccess('Entry deleted.');
        this.router.navigate(['/spaces', this.spaceId]);
      },
      error: () => this.notification.showError('Could not delete the entry.')
    });
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file || !this.entryId) return;

    this.spacesService.uploadAttachment(this.spaceId, file, undefined, this.entryId).subscribe({
      next: (attachment) => {
        this.attachments = [attachment, ...this.attachments];
        this.notification.showSuccess('Attachment uploaded.');
      },
      error: () => this.notification.showError('Could not upload the attachment.')
    });
    input.value = '';
  }

  attachmentUrl(attachment: Attachment): string {
    return this.spacesService.attachmentUrl(this.spaceId, attachment.id);
  }

  deleteAttachment(attachment: Attachment): void {
    this.spacesService.deleteAttachment(this.spaceId, attachment.id).subscribe({
      next: () => (this.attachments = this.attachments.filter(a => a.id !== attachment.id)),
      error: () => this.notification.showError('Could not delete the attachment.')
    });
  }

  private parseFields(json: string): Record<string, string> {
    try {
      return JSON.parse(json || '{}');
    } catch {
      return {};
    }
  }

  cancel(): void {
    this.router.navigate(['/spaces', this.spaceId]);
  }
}
