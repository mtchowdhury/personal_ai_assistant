import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { SpacesService, Space, SearchResult } from '@features/application/spaces/services/spaces.service';
import { NotificationService } from '@core/services/notification.service';
import { LoadingSpinnerComponent } from '@core/components/loading-spinner/loading-spinner.component';

@Component({
  selector: 'app-space-search',
  standalone: true,
  imports: [CommonModule, FormsModule, LoadingSpinnerComponent],
  templateUrl: './space-search.component.html',
  styleUrls: ['./space-search.component.scss']
})
export class SpaceSearchComponent implements OnInit {
  query = '';
  spaceId: string | null = null;
  mode: 'hybrid' | 'text' | 'semantic' = 'hybrid';
  spaces: Space[] = [];
  results: SearchResult[] = [];
  hasSearched = false;
  isSearching = false;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private spacesService: SpacesService,
    private notification: NotificationService
  ) {}

  ngOnInit(): void {
    this.spaceId = this.route.snapshot.queryParamMap.get('spaceId');
    this.spacesService.getSpaces().subscribe({
      next: (spaces) => (this.spaces = spaces),
      error: () => {}
    });
  }

  search(): void {
    const term = this.query.trim();
    this.isSearching = true;
    this.hasSearched = true;

    this.spacesService.search({
      spaceId: this.spaceId || undefined,
      query: term || undefined,
      mode: this.mode,
      limit: 30
    }).subscribe({
      next: (results) => {
        this.results = results;
        this.isSearching = false;
      },
      error: () => {
        this.isSearching = false;
        this.notification.showError('Search failed.');
      }
    });
  }

  openResult(r: SearchResult): void {
    this.router.navigate(['/spaces', r.spaceId, 'entries', r.entryId]);
  }

  cancel(): void {
    this.router.navigate(['/spaces']);
  }
}
