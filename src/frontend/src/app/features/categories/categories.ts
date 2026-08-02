import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { CategoryDto } from '../../core/models/category.model';
import { CategoryApiService } from '../../core/api/category-api.service';

// Categories (dashboard-ux-brief.md §4.4, level 1) - a grid of clickable category tiles. No
// filter/sort bar here (Constraints) - this is a navigation surface into the drill-down, not a
// filterable repository list itself.
@Component({
  selector: 'app-categories',
  imports: [RouterLink, MatButtonModule, MatCardModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './categories.html',
  styleUrl: './categories.scss',
})
export class Categories implements OnInit {
  private readonly categoryApi = inject(CategoryApiService);

  protected readonly categories = signal<CategoryDto[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal(false);

  ngOnInit(): void {
    this.fetch();
  }

  protected onRetry(): void {
    this.fetch();
  }

  protected tileVariant(index: number): 'terracotta' | 'olive' {
    return index % 2 === 0 ? 'terracotta' : 'olive';
  }

  private fetch(): void {
    this.loading.set(true);
    this.error.set(false);

    this.categoryApi.getCategories().subscribe({
      next: (result) => {
        this.categories.set(result.categories);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set(true);
      },
    });
  }
}
