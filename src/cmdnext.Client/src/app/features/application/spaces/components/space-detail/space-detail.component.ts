import { AfterViewChecked, Component, ElementRef, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  SpacesService, Space, Node, EntryListItem, EntryTypeSchema, parseEntryTypeSchema
} from '@features/application/spaces/services/spaces.service';
import { NotificationService } from '@core/services/notification.service';
import { LoadingSpinnerComponent } from '@core/components/loading-spinner/loading-spinner.component';

interface TreeNode extends Node {
  children: TreeNode[];
  depth: number;
}

@Component({
  selector: 'app-space-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LoadingSpinnerComponent],
  templateUrl: './space-detail.component.html',
  styleUrls: ['./space-detail.component.scss']
})
export class SpaceDetailComponent implements OnInit, OnDestroy, AfterViewChecked {
  spaceId = '';
  space: Space | null = null;
  isLoading = true;

  allNodes: Node[] = [];
  tree: TreeNode[] = [];
  flatTree: TreeNode[] = [];
  selectedNodeId: string | null = null;

  entries: EntryListItem[] = [];
  entriesLoading = false;

  entryTypes: EntryTypeSchema[] = [];

  // Inline "add node" form
  @ViewChild('newNodeInput') newNodeInput?: ElementRef<HTMLInputElement>;
  @ViewChild('entriesScroll') entriesScroll?: ElementRef<HTMLElement>;
  showAddNode = false;
  newNodeName = '';
  /** Set when the form opens so ngAfterViewChecked focuses the input exactly once. */
  private focusNewNodeInput = false;

  /**
   * Scroll offset of the entry list to restore once the entries have rendered, or null when
   * nothing is pending. Opening an entry destroys this component (separate route), so the
   * offset is parked in sessionStorage and picked up on the way back — otherwise returning
   * from the editor snaps the user to the top of a long list.
   */
  private pendingScrollTop: number | null = null;

  // Drag & drop: entry being dragged, and the tree target currently hovered.
  // `dropTargetId` uses '' for the "All entries" (space root) target, since null means "nothing
  // hovered" — the two need to stay distinguishable.
  draggingEntryId: string | null = null;
  dropTargetId: string | null = null;
  /** Offscreen element used as the drag image; removed once the browser has snapshotted it. */
  private dragImageEl: HTMLElement | null = null;

  /** node id -> "Parent / Child" display label, rebuilt whenever the tree loads. */
  private nodeLabels = new Map<string, string>();

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private spacesService: SpacesService,
    private notification: NotificationService
  ) {}

  ngOnInit(): void {
    this.spaceId = this.route.snapshot.paramMap.get('id') ?? '';
    this.takeStoredScroll();
    this.load();
  }

  ngOnDestroy(): void {
    // Navigating away mid-drag would otherwise leave the chip attached to <body>.
    this.removeDragImage();
  }

  private load(): void {
    this.isLoading = true;
    this.spacesService.getSpace(this.spaceId).subscribe({
      next: (space) => {
        this.space = space;
        this.entryTypes = parseEntryTypeSchema(space.schemaJson);
        this.loadNodes();
      },
      error: () => {
        this.isLoading = false;
        this.notification.showError('Could not load this space.');
      }
    });
  }

  private loadNodes(): void {
    this.spacesService.getNodes(this.spaceId).subscribe({
      next: (nodes) => {
        this.allNodes = nodes;
        this.buildTree();
        this.isLoading = false;
        this.loadEntries();
      },
      error: () => {
        this.isLoading = false;
        this.notification.showError('Could not load the node tree.');
      }
    });
  }

  private buildTree(): void {
    const byId = new Map<string, TreeNode>();
    for (const n of this.allNodes) byId.set(n.id, { ...n, children: [], depth: 0 });

    const roots: TreeNode[] = [];
    for (const n of byId.values()) {
      if (n.parentId && byId.has(n.parentId)) {
        const parent = byId.get(n.parentId)!;
        n.depth = parent.depth + 1;
        parent.children.push(n);
      } else {
        roots.push(n);
      }
    }

    // Alphabetic by name, so a node is findable in a long tree. localeCompare with numeric
    // collation keeps "Class 2" before "Class 10".
    const collator = new Intl.Collator(undefined, { numeric: true, sensitivity: 'base' });
    const sortRec = (list: TreeNode[]) => {
      list.sort((a, b) => collator.compare(a.name, b.name));
      for (const n of list) sortRec(n.children);
    };
    sortRec(roots);

    this.tree = roots;
    this.flatTree = [];
    const flatten = (list: TreeNode[]) => {
      for (const n of list) {
        this.flatTree.push(n);
        flatten(n.children);
      }
    };
    flatten(roots);

    this.buildNodeLabels();
  }

  selectNode(nodeId: string | null): void {
    this.selectedNodeId = nodeId;
    // Switching nodes is a deliberate context change — show the new list from the top.
    this.pendingScrollTop = null;
    if (this.entriesScroll) this.entriesScroll.nativeElement.scrollTop = 0;
    this.loadEntries();
  }

  /** sessionStorage key for the current space + selected node. */
  private scrollKey(): string {
    return `spaces:${this.spaceId}:${this.selectedNodeId ?? 'root'}:scroll`;
  }

  /** Holds the offset across an in-place reload; this component stays alive. */
  private rememberEntriesScroll(): void {
    const el = this.entriesScroll?.nativeElement;
    if (el) this.pendingScrollTop = el.scrollTop;
  }

  /**
   * Parks the offset for a reload that destroys this component — opening an entry is a
   * separate route, so in-memory state does not survive the trip.
   */
  private parkEntriesScroll(): void {
    const el = this.entriesScroll?.nativeElement;
    if (!el) return;
    try {
      sessionStorage.setItem(this.scrollKey(), String(el.scrollTop));
    } catch {
      // Private-mode browsers can throw on write; a lost scroll position is not worth failing over.
    }
  }

  /** Picks up an offset parked by a previous visit to this space/node. */
  private takeStoredScroll(): void {
    try {
      const raw = sessionStorage.getItem(this.scrollKey());
      if (raw === null) return;
      sessionStorage.removeItem(this.scrollKey());
      const value = Number(raw);
      if (Number.isFinite(value) && value > 0) this.pendingScrollTop = value;
    } catch {
      // No stored position available; start at the top.
    }
  }

  private restoreEntriesScrollIfPending(): void {
    if (this.pendingScrollTop === null || this.entriesLoading) return;

    const el = this.entriesScroll?.nativeElement;
    if (!el) return;

    // While loading, the list is hidden and the container collapses; assigning scrollTop then
    // would be clamped to 0 and the position lost. Wait until it can actually hold the offset.
    const maxScroll = el.scrollHeight - el.clientHeight;
    if (maxScroll <= 0) return;

    el.scrollTop = Math.min(this.pendingScrollTop, maxScroll);
    this.pendingScrollTop = null;
  }

  private loadEntries(): void {
    this.entriesLoading = true;
    this.spacesService.getEntries(this.spaceId, {
      nodeId: this.selectedNodeId ?? undefined,
      includeDescendants: true,
      take: 200
    }).subscribe({
      next: (entries) => {
        this.entries = entries;
        this.entriesLoading = false;
      },
      error: () => {
        this.entriesLoading = false;
        this.notification.showError('Could not load entries.');
      }
    });
  }

  selectedNode(): TreeNode | undefined {
    return this.flatTree.find(n => n.id === this.selectedNodeId);
  }

  toggleAddNode(): void {
    this.showAddNode = !this.showAddNode;
    // The input is behind *ngIf, so it cannot be focused until the view has rendered it.
    this.focusNewNodeInput = this.showAddNode;
    if (!this.showAddNode) this.newNodeName = '';
  }

  ngAfterViewChecked(): void {
    if (this.focusNewNodeInput && this.newNodeInput) {
      this.focusNewNodeInput = false;
      this.newNodeInput.nativeElement.focus();
    }
    this.restoreEntriesScrollIfPending();
  }

  addNode(): void {
    const name = this.newNodeName.trim();
    if (!name) return;

    this.spacesService.createNode(this.spaceId, {
      parentId: this.selectedNodeId,
      name,
      kind: this.space?.kind === 'people' ? 'person' : undefined
    }).subscribe({
      next: () => {
        this.newNodeName = '';
        this.showAddNode = false;
        this.loadNodes();
      },
      error: () => this.notification.showError('Could not create the node.')
    });
  }

  addEntry(): void {
    this.parkEntriesScroll();
    this.router.navigate(['/spaces', this.spaceId, 'entries', 'new'], {
      queryParams: { nodeId: this.selectedNodeId ?? undefined }
    });
  }

  // ---- Drag & drop: move an entry into a node ----

  onEntryDragStart(entry: EntryListItem, event: DragEvent): void {
    this.draggingEntryId = entry.id;
    if (!event.dataTransfer) return;

    event.dataTransfer.effectAllowed = 'move';
    // Firefox only starts a drag when some data is set.
    event.dataTransfer.setData('text/plain', entry.id);
    this.useCompactDragImage(entry, event);
  }

  /**
   * Replaces the browser's default drag image — a full-size snapshot of the entry card, wide
   * enough to blanket the 240px tree column and hide which node you are over — with a small
   * title-only chip that sits just off the cursor.
   */
  private useCompactDragImage(entry: EntryListItem, event: DragEvent): void {
    // setDragImage needs the element rendered, so it is attached offscreen and removed once
    // the browser has taken its snapshot.
    const chip = document.createElement('div');
    chip.className = 'entry-drag-chip';
    chip.textContent = entry.title;
    chip.style.position = 'fixed';
    chip.style.top = '-1000px';
    chip.style.left = '-1000px';
    document.body.appendChild(chip);
    this.dragImageEl = chip;

    try {
      event.dataTransfer!.setDragImage(chip, 16, 16);
    } catch {
      // Safari/older browsers fall back to the default image; nothing else to do.
    }

    // The snapshot is taken synchronously right after this handler returns.
    setTimeout(() => this.removeDragImage());
  }

  private removeDragImage(): void {
    this.dragImageEl?.remove();
    this.dragImageEl = null;
  }

  onEntryDragEnd(): void {
    this.draggingEntryId = null;
    this.dropTargetId = null;
    this.removeDragImage();
  }

  /** nodeId null = the space root ("All entries"). */
  onNodeDragOver(nodeId: string | null, event: DragEvent): void {
    if (!this.draggingEntryId) return;
    // Only preventDefault marks this element as a valid drop target.
    event.preventDefault();
    if (event.dataTransfer) event.dataTransfer.dropEffect = 'move';
    this.dropTargetId = nodeId ?? '';
  }

  onNodeDragLeave(nodeId: string | null): void {
    if (this.dropTargetId === (nodeId ?? '')) this.dropTargetId = null;
  }

  onNodeDrop(nodeId: string | null, event: DragEvent): void {
    event.preventDefault();

    const entryId = this.draggingEntryId ?? event.dataTransfer?.getData('text/plain') ?? null;
    this.draggingEntryId = null;
    this.dropTargetId = null;
    if (!entryId) return;

    const entry = this.entries.find(e => e.id === entryId);
    if (entry && (entry.nodeId ?? null) === nodeId) return;

    // The reload below re-renders the list; keep the user where they were dragging from.
    this.rememberEntriesScroll();

    this.spacesService.moveEntry(this.spaceId, entryId, nodeId).subscribe({
      next: () => {
        this.notification.showSuccess(
          nodeId ? `Moved to ${this.nodeName(nodeId)}.` : 'Moved to the space root.');
        // Counts in the tree and the visible list both change.
        this.loadNodes();
      },
      error: () => this.notification.showError('Could not move the entry.')
    });
  }

  private nodeName(nodeId: string): string {
    return this.flatTree.find(n => n.id === nodeId)?.name ?? 'the node';
  }

  /**
   * Label for an entry's node chip. Uses the node's display name rather than the entry's
   * `nodePath`, which is a slug ("mohiner-ghoraguli"); nested nodes show their ancestors so a
   * bare child name is not ambiguous. Returns null for entries sitting at the space root.
   *
   * Reads a map built once per tree load — this is called from the template for every visible
   * card on each change-detection pass (including every mousemove during a drag), so it must
   * not walk the node list.
   */
  entryNodeLabel(entry: EntryListItem): string | null {
    if (!entry.nodeId) return null;
    // Fall back to the slug path if the tree has not loaded or the node is missing from it.
    return this.nodeLabels.get(entry.nodeId) ?? entry.nodePath ?? null;
  }

  /** Builds "Parent / Child" display labels for every node in the tree. */
  private buildNodeLabels(): void {
    const byId = new Map(this.flatTree.map(n => [n.id, n]));
    this.nodeLabels = new Map();

    for (const node of this.flatTree) {
      const names: string[] = [];
      let current: TreeNode | undefined = node;
      // Depth-bounded: the tree is built from the same list, so this cannot cycle, but stay safe.
      while (current && names.length <= 10) {
        names.unshift(current.name);
        current = current.parentId ? byId.get(current.parentId) : undefined;
      }
      this.nodeLabels.set(node.id, names.join(' / '));
    }
  }

  openEntry(entry: EntryListItem): void {
    this.parkEntriesScroll();
    this.router.navigate(['/spaces', this.spaceId, 'entries', entry.id]);
  }

  stateEntries(): { key: string; value: string }[] {
    if (!this.space) return [];
    try {
      const state = JSON.parse(this.space.stateJson || '{}');
      return Object.entries(state).map(([key, value]) => ({ key, value: String(value) }));
    } catch {
      return [];
    }
  }

  goBack(): void {
    this.router.navigate(['/spaces']);
  }
}
