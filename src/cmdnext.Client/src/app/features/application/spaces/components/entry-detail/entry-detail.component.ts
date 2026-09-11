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

  /**
   * Click handler on the rendered body. Flipping a checkbox rewrites the corresponding
   * "- [ ]" / "- [x]" marker in the markdown and saves, so imported task notes are usable
   * without dropping into the raw editor.
   *
   * Bound on the container rather than per-checkbox because the HTML comes from a pipe via
   * innerHTML, where Angular bindings do not apply.
   */
  onRenderedBodyClick(event: Event): void {
    const target = event.target as HTMLElement;
    if (!target?.classList?.contains('task-checkbox')) {
      // Clicking anywhere else in the rendered body still opens the editor, as before.
      this.toggleEditBody();
      return;
    }

    // Don't let the click fall through to the edit toggle.
    event.preventDefault();
    event.stopPropagation();

    const index = Number(target.getAttribute('data-task-index'));
    if (!Number.isInteger(index)) return;

    const updated = this.toggleTaskAt(this.body, index);
    if (updated === null) return;

    this.body = updated;
    this.saveBodyOnly();
  }

  /**
   * Flips the nth task marker in the markdown, counting in the same order the pipe numbered
   * them. Returns null when the index does not resolve, so a stale render cannot rewrite an
   * unrelated line.
   */
  private toggleTaskAt(markdown: string, index: number): string | null {
    const taskLine = /^(\s*(?:[-*+]|\d+[.)])\s+\[)([ xX])(\]\s+.*)$/;
    const lines = markdown.split('\n');
    let seen = 0;

    for (let i = 0; i < lines.length; i++) {
      const m = taskLine.exec(lines[i]);
      if (!m) continue;
      if (seen === index) {
        const checked = m[2].toLowerCase() === 'x';
        lines[i] = `${m[1]}${checked ? ' ' : 'x'}${m[3]}`;
        return lines.join('\n');
      }
      seen++;
    }
    return null;
  }

  /** Persists just the body — used by the checkbox toggle, which is not a full form save. */
  private saveBodyOnly(): void {
    if (this.isNew || !this.entryId) return;

    this.spacesService.updateEntry(this.spaceId, this.entryId, { body: this.body }).subscribe({
      next: (entry) => this.applyEntry(entry),
      error: () => this.notification.showError('Could not update the checklist.')
    });
  }

  // ---- Formatting toolbar ----
  //
  // Acts on the textarea's current selection and writes plain markdown, so the stored format
  // stays exactly what the search index, the AI tools and the Flutter app already read.

  /** Wraps the selection in `marker` (bold, italic, inline code), or unwraps it if already wrapped. */
  wrapSelection(marker: string): void {
    const el = this.bodyTextarea?.nativeElement;
    if (!el) return;

    const { selectionStart: start, selectionEnd: end } = el;
    const selected = this.body.slice(start, end);
    const before = this.body.slice(0, start);
    const after = this.body.slice(end);

    // Toggle off when the markers are already there, so clicking twice is a no-op.
    if (before.endsWith(marker) && after.startsWith(marker)) {
      this.body = before.slice(0, -marker.length) + selected + after.slice(marker.length);
      this.restoreSelection(el, start - marker.length, end - marker.length);
      return;
    }

    const placeholder = selected || 'text';
    this.body = `${before}${marker}${placeholder}${marker}${after}`;
    // Select the inner text so typing replaces the placeholder immediately.
    this.restoreSelection(el, start + marker.length, start + marker.length + placeholder.length);
  }

  /**
   * Applies a line prefix (heading, bullet, number, task, quote) to every line the selection
   * touches. Re-applying the same prefix strips it, so the buttons toggle.
   */
  prefixLines(prefix: string): void {
    const el = this.bodyTextarea?.nativeElement;
    if (!el) return;

    const { selectionStart: start, selectionEnd: end } = el;
    // Expand the range to whole lines — a prefix only makes sense line-wise.
    const from = this.body.lastIndexOf('\n', start - 1) + 1;
    const toIndex = this.body.indexOf('\n', end);
    const to = toIndex === -1 ? this.body.length : toIndex;

    const lines = this.body.slice(from, to).split('\n');
    // Ordered lists renumber, so compare against the generic shape rather than the literal.
    const isNumbered = prefix === '1. ';
    const existing = isNumbered ? /^\d+[.)]\s+/ : null;

    // Only lines with content count when deciding whether this is a toggle-off. A blank line
    // would otherwise satisfy `every` vacuously, so clicking the button on an empty line read
    // as "already a list, strip it" and did nothing — the case where you want to start a list
    // and then type into it.
    const contentLines = lines.filter(l => l.trim() !== '');
    const allPrefixed = contentLines.length > 0 && contentLines.every(l =>
      existing ? existing.test(l) : l.startsWith(prefix));

    const updated = lines.map((line, i) => {
      if (allPrefixed) {
        if (line.trim() === '') return line;
        return existing ? line.replace(existing, '') : line.slice(prefix.length);
      }
      // An empty line gets a bare marker to type into; a blank line inside a multi-line
      // selection stays blank rather than becoming a stray empty bullet.
      if (line.trim() === '') {
        return lines.length === 1 ? (isNumbered ? '1. ' : prefix) : line;
      }
      // Strip any prefix already present so the markers don't stack up.
      const bare = line.replace(/^(#{1,6}\s+|[-*+]\s+\[[ xX]\]\s+|[-*+]\s+|\d+[.)]\s+|>\s+)/, '');
      return `${isNumbered ? `${i + 1}. ` : prefix}${bare}`;
    });

    const replacement = updated.join('\n');
    this.body = this.body.slice(0, from) + replacement + this.body.slice(to);

    // Starting a fresh list: put the caret after the marker, ready to type. Otherwise keep
    // the affected lines selected so the change is visible and re-clickable.
    if (lines.length === 1 && lines[0].trim() === '' && !allPrefixed) {
      const caret = from + replacement.length;
      this.restoreSelection(el, caret, caret);
    } else {
      this.restoreSelection(el, from, from + replacement.length);
    }
  }

  /** Inserts a markdown link around the selection, using it as the link text. */
  insertLink(): void {
    const el = this.bodyTextarea?.nativeElement;
    if (!el) return;

    const { selectionStart: start, selectionEnd: end } = el;
    const selected = this.body.slice(start, end) || 'text';
    const snippet = `[${selected}](url)`;

    this.body = this.body.slice(0, start) + snippet + this.body.slice(end);
    // Put the caret on "url" so the address can be typed straight away.
    const urlAt = start + selected.length + 3;
    this.restoreSelection(el, urlAt, urlAt + 3);
  }

  /**
   * Re-applies the caret after Angular has written the new value back to the textarea.
   * Setting `this.body` alone would leave the caret at the end of the field.
   */
  private restoreSelection(el: HTMLTextAreaElement, start: number, end: number): void {
    setTimeout(() => {
      el.focus();
      el.setSelectionRange(start, end);
      el.style.height = 'auto';
      el.style.height = `${el.scrollHeight}px`;
    });
  }

  /** Ctrl/Cmd+B and Ctrl/Cmd+I, plus list continuation on Enter. */
  onBodyKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey && !event.ctrlKey && !event.metaKey) {
      this.continueListOnEnter(event);
      return;
    }

    if (!(event.ctrlKey || event.metaKey) || event.altKey) return;

    const key = event.key.toLowerCase();
    if (key === 'b') {
      event.preventDefault();
      this.wrapSelection('**');
    } else if (key === 'i') {
      event.preventDefault();
      this.wrapSelection('*');
    }
  }

  /**
   * Enter inside a list item starts the next item with the same marker; Enter on an item that
   * is still empty ends the list instead (the usual "press Enter twice to get out" motion).
   */
  private continueListOnEnter(event: KeyboardEvent): void {
    const el = this.bodyTextarea?.nativeElement;
    if (!el || el.selectionStart !== el.selectionEnd) return;

    const caret = el.selectionStart;
    const lineStart = this.body.lastIndexOf('\n', caret - 1) + 1;
    const line = this.body.slice(lineStart, caret);

    // indent + marker + content. A task marker is matched before a plain bullet so that
    // "- [ ] " continues as a task rather than as "- ".
    const m = /^(\s*)(?:([-*+])\s+\[[ xX]\]\s+|([-*+])\s+|(\d+)([.)])\s+)(.*)$/.exec(line);
    if (!m) return;

    const [, indent, taskBullet, bullet, number, numberDelim, content] = m;

    // Empty item: drop the marker and end the list rather than adding another.
    if (content.trim() === '') {
      event.preventDefault();
      this.body = this.body.slice(0, lineStart) + this.body.slice(caret);
      this.restoreSelection(el, lineStart, lineStart);
      return;
    }

    let marker: string;
    if (taskBullet) marker = `${taskBullet} [ ] `;
    else if (bullet) marker = `${bullet} `;
    else marker = `${Number(number) + 1}${numberDelim} `;

    event.preventDefault();
    const insert = `\n${indent}${marker}`;
    this.body = this.body.slice(0, caret) + insert + this.body.slice(caret);
    const next = caret + insert.length;
    this.restoreSelection(el, next, next);
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

  /**
   * Opens an attachment in a new tab. The endpoint is authenticated and the token lives in
   * localStorage rather than a cookie, so a plain href navigation is unauthenticated and 401s;
   * fetch the bytes through HttpClient (interceptor adds the header) and open a blob URL.
   */
  openAttachment(attachment: Attachment): void {
    if (this.downloadingAttachmentId) return;
    this.downloadingAttachmentId = attachment.id;

    this.spacesService.downloadAttachment(this.spaceId, attachment.id).subscribe({
      next: (blob) => {
        this.downloadingAttachmentId = null;
        // Re-tag the blob with the stored content type: the browser picks the viewer from it,
        // and without it a PDF downloads instead of opening.
        const typed = blob.type ? blob : new Blob([blob], { type: attachment.contentType });
        const url = URL.createObjectURL(typed);
        const opened = window.open(url, '_blank');

        if (!opened) {
          // Popup blocked — fall back to a direct download so the click still does something.
          const link = document.createElement('a');
          link.href = url;
          link.download = attachment.fileName;
          link.click();
        }

        // Revoking immediately would cancel the load in the new tab; give it time to read.
        setTimeout(() => URL.revokeObjectURL(url), 60_000);
      },
      error: () => {
        this.downloadingAttachmentId = null;
        this.notification.showError('Could not open that attachment.');
      }
    });
  }

  /** Id of the attachment currently being fetched, so the row can show progress. */
  downloadingAttachmentId: string | null = null;

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
