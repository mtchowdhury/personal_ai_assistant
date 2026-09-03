import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  SpacesService, Space, Node, EntryListItem, EntryTypeSchema
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
export class SpaceDetailComponent implements OnInit {
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
  showAddNode = false;
  newNodeName = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private spacesService: SpacesService,
    private notification: NotificationService
  ) {}

  ngOnInit(): void {
    this.spaceId = this.route.snapshot.paramMap.get('id') ?? '';
    this.load();
  }

  private load(): void {
    this.isLoading = true;
    this.spacesService.getSpace(this.spaceId).subscribe({
      next: (space) => {
        this.space = space;
        this.entryTypes = this.parseSchema(space.schemaJson);
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

    const sortRec = (list: TreeNode[]) => {
      list.sort((a, b) => a.sortOrder - b.sortOrder);
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
  }

  selectNode(nodeId: string | null): void {
    this.selectedNodeId = nodeId;
    this.loadEntries();
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
    this.router.navigate(['/spaces', this.spaceId, 'entries', 'new'], {
      queryParams: { nodeId: this.selectedNodeId ?? undefined }
    });
  }

  openEntry(entry: EntryListItem): void {
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

  private parseSchema(json: string): EntryTypeSchema[] {
    try {
      return JSON.parse(json || '[]');
    } catch {
      return [];
    }
  }

  goBack(): void {
    this.router.navigate(['/spaces']);
  }
}
