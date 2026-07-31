import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';

// Scaffolding placeholder (F-003): proves Angular Material is installed, themed, and renders via
// a standalone component. Real dashboard views (Discovery Feed, Hidden Gems, Trending,
// Categories) are later features, not part of this scaffold.
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, MatToolbarModule],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly title = 'GitHub Hidden Gems Discovery Platform';
}
