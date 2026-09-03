import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';

export interface FieldSchema {
  name: string;
  type: string;
  required: boolean;
  options?: string[] | null;
}

export interface EntryTypeSchema {
  type: string;
  label: string;
  fields: FieldSchema[];
}

export interface SpaceTemplate {
  kind: string;
  label: string;
  description: string;
  suggestedNodeKind?: string | null;
  entryTypes: EntryTypeSchema[];
}

export interface Space {
  id: string;
  name: string;
  slug: string;
  kind: string;
  description?: string | null;
  conventions?: string | null;
  settingsJson: string;
  stateJson: string;
  schemaJson: string;
  status: string;
  createdOn?: string | null;
  updatedOn?: string | null;
}

export interface CreateSpaceRequest {
  name: string;
  kind: string;
  description?: string | null;
  conventions?: string | null;
}

export interface UpdateSpaceRequest {
  name?: string | null;
  description?: string | null;
  conventions?: string | null;
  settingsJson?: string | null;
  status?: string | null;
}

export interface Node {
  id: string;
  spaceId: string;
  parentId?: string | null;
  name: string;
  path: string;
  sortOrder: number;
  kind?: string | null;
  summary?: string | null;
  entryCount: number;
  childCount: number;
}

export interface CreateNodeRequest {
  parentId?: string | null;
  name: string;
  kind?: string | null;
  summary?: string | null;
}

export interface UpdateNodeRequest {
  name?: string | null;
  parentId?: string | null;
  parentIdSet?: boolean;
  summary?: string | null;
  sortOrder?: number | null;
}

export interface Entry {
  id: string;
  spaceId: string;
  nodeId?: string | null;
  nodePath?: string | null;
  type: string;
  title: string;
  body: string;
  fieldsJson: string;
  tags: string[];
  occurredOn?: string | null;
  dueOn?: string | null;
  status?: string | null;
  source: string;
  createdOn?: string | null;
  updatedOn?: string | null;
  attachmentCount: number;
}

export interface EntryListItem {
  id: string;
  nodeId?: string | null;
  nodePath?: string | null;
  type: string;
  title: string;
  excerpt: string;
  tags: string[];
  occurredOn?: string | null;
  dueOn?: string | null;
  status?: string | null;
  source: string;
  createdOn?: string | null;
}

export interface CreateEntryRequest {
  nodeId?: string | null;
  type: string;
  title: string;
  body: string;
  fieldsJson?: string | null;
  tags?: string[] | null;
  occurredOn?: string | null;
  dueOn?: string | null;
  status?: string | null;
  source?: string | null;
}

export interface UpdateEntryRequest {
  nodeId?: string | null;
  nodeIdSet?: boolean;
  title?: string | null;
  body?: string | null;
  fieldsJson?: string | null;
  tags?: string[] | null;
  occurredOn?: string | null;
  dueOn?: string | null;
  status?: string | null;
}

export interface AppendToEntryRequest {
  text: string;
  heading?: string | null;
}

export interface EntryQuery {
  nodeId?: string | null;
  includeDescendants?: boolean;
  type?: string | null;
  tags?: string[] | null;
  status?: string | null;
  from?: string | null;
  to?: string | null;
  take?: number;
}

export interface SearchEntriesRequest {
  spaceId?: string | null;
  nodeId?: string | null;
  includeDescendants?: boolean;
  query?: string | null;
  type?: string | null;
  tags?: string[] | null;
  from?: string | null;
  to?: string | null;
  mode?: string;
  limit?: number;
}

export interface SearchResult {
  entryId: string;
  spaceId: string;
  spaceName: string;
  nodeId?: string | null;
  nodePath?: string | null;
  type: string;
  title: string;
  snippet: string;
  occurredOn?: string | null;
  rank: number;
}

export interface Attachment {
  id: string;
  spaceId: string;
  nodeId?: string | null;
  entryId?: string | null;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  storageProvider: string;
  createdOn?: string | null;
}

@Injectable({ providedIn: 'root' })
export class SpacesService {
  private readonly baseUrl = `${environment.apiUrl}/Spaces`;

  constructor(private http: HttpClient) {}

  // ---- Templates ----
  getTemplates(): Observable<SpaceTemplate[]> {
    return this.http.get<SpaceTemplate[]>(`${this.baseUrl}/templates`);
  }

  // ---- Spaces ----
  getSpaces(includeArchived = false): Observable<Space[]> {
    return this.http.get<Space[]>(`${this.baseUrl}?includeArchived=${includeArchived}`);
  }

  getSpace(id: string): Observable<Space> {
    return this.http.get<Space>(`${this.baseUrl}/${id}`);
  }

  createSpace(request: CreateSpaceRequest): Observable<Space> {
    return this.http.post<Space>(this.baseUrl, request);
  }

  updateSpace(id: string, request: UpdateSpaceRequest): Observable<Space> {
    return this.http.put<Space>(`${this.baseUrl}/${id}`, request);
  }

  updateSpaceState(id: string, statePatchJson: string): Observable<Space> {
    return this.http.put<Space>(`${this.baseUrl}/${id}/state`, { statePatchJson });
  }

  updateSpaceSchema(id: string, schemaJson: string): Observable<Space> {
    return this.http.put<Space>(`${this.baseUrl}/${id}/schema`, { schemaJson });
  }

  archiveSpace(id: string): Observable<unknown> {
    return this.http.delete(`${this.baseUrl}/${id}`);
  }

  deleteSpace(id: string): Observable<unknown> {
    return this.http.delete(`${this.baseUrl}/${id}?hard=true`);
  }

  // ---- Nodes ----
  getNodes(spaceId: string): Observable<Node[]> {
    return this.http.get<Node[]>(`${this.baseUrl}/${spaceId}/nodes`);
  }

  createNode(spaceId: string, request: CreateNodeRequest): Observable<Node> {
    return this.http.post<Node>(`${this.baseUrl}/${spaceId}/nodes`, request);
  }

  updateNode(spaceId: string, nodeId: string, request: UpdateNodeRequest): Observable<Node> {
    return this.http.put<Node>(`${this.baseUrl}/${spaceId}/nodes/${nodeId}`, request);
  }

  deleteNode(spaceId: string, nodeId: string): Observable<unknown> {
    return this.http.delete(`${this.baseUrl}/${spaceId}/nodes/${nodeId}`);
  }

  // ---- Entries ----
  getEntries(spaceId: string, query: EntryQuery = {}): Observable<EntryListItem[]> {
    const params: string[] = [];
    if (query.nodeId) params.push(`NodeId=${query.nodeId}`);
    if (query.includeDescendants) params.push(`IncludeDescendants=true`);
    if (query.type) params.push(`Type=${encodeURIComponent(query.type)}`);
    if (query.status) params.push(`Status=${encodeURIComponent(query.status)}`);
    if (query.from) params.push(`From=${encodeURIComponent(query.from)}`);
    if (query.to) params.push(`To=${encodeURIComponent(query.to)}`);
    if (query.take) params.push(`Take=${query.take}`);
    for (const tag of query.tags ?? []) params.push(`Tags=${encodeURIComponent(tag)}`);
    const q = params.length ? `?${params.join('&')}` : '';
    return this.http.get<EntryListItem[]>(`${this.baseUrl}/${spaceId}/entries${q}`);
  }

  getEntry(spaceId: string, entryId: string): Observable<Entry> {
    return this.http.get<Entry>(`${this.baseUrl}/${spaceId}/entries/${entryId}`);
  }

  createEntry(spaceId: string, request: CreateEntryRequest): Observable<Entry> {
    return this.http.post<Entry>(`${this.baseUrl}/${spaceId}/entries`, request);
  }

  updateEntry(spaceId: string, entryId: string, request: UpdateEntryRequest): Observable<Entry> {
    return this.http.put<Entry>(`${this.baseUrl}/${spaceId}/entries/${entryId}`, request);
  }

  appendToEntry(spaceId: string, entryId: string, request: AppendToEntryRequest): Observable<Entry> {
    return this.http.post<Entry>(`${this.baseUrl}/${spaceId}/entries/${entryId}/append`, request);
  }

  deleteEntry(spaceId: string, entryId: string): Observable<unknown> {
    return this.http.delete(`${this.baseUrl}/${spaceId}/entries/${entryId}`);
  }

  // ---- Search ----
  search(request: SearchEntriesRequest): Observable<SearchResult[]> {
    return this.http.post<SearchResult[]>(`${this.baseUrl}/search`, request);
  }

  // ---- Attachments ----
  uploadAttachment(spaceId: string, file: File, nodeId?: string | null, entryId?: string | null): Observable<Attachment> {
    const form = new FormData();
    form.append('file', file);
    const params: string[] = [];
    if (nodeId) params.push(`nodeId=${nodeId}`);
    if (entryId) params.push(`entryId=${entryId}`);
    const q = params.length ? `?${params.join('&')}` : '';
    return this.http.post<Attachment>(`${this.baseUrl}/${spaceId}/attachments${q}`, form);
  }

  getAttachments(spaceId: string, nodeId?: string | null, entryId?: string | null): Observable<Attachment[]> {
    const params: string[] = [];
    if (nodeId) params.push(`nodeId=${nodeId}`);
    if (entryId) params.push(`entryId=${entryId}`);
    const q = params.length ? `?${params.join('&')}` : '';
    return this.http.get<Attachment[]>(`${this.baseUrl}/${spaceId}/attachments${q}`);
  }

  attachmentUrl(spaceId: string, attachmentId: string): string {
    return `${this.baseUrl}/${spaceId}/attachments/${attachmentId}`;
  }

  deleteAttachment(spaceId: string, attachmentId: string): Observable<unknown> {
    return this.http.delete(`${this.baseUrl}/${spaceId}/attachments/${attachmentId}`);
  }
}
