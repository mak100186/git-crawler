import { inject, Injectable } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';
import { MatIconRegistry } from '@angular/material/icon';

import { registerAppIcons } from './icon-registry.service';

@Injectable({ providedIn: 'root' })
export class TestIconRegistry {
  constructor() {
    const iconRegistry = inject(MatIconRegistry);
    const sanitizer = inject(DomSanitizer);
    registerAppIcons(iconRegistry, sanitizer);
  }
}
