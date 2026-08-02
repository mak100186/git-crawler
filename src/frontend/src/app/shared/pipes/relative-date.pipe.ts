import { Pipe, PipeTransform } from '@angular/core';

// "Discovered/last-updated date (relative, e.g., '2 days ago')" - dashboard-ux-brief.md §4.1.
// A small hand-rolled pipe rather than a date library dependency; the design only needs coarse
// relative buckets (minutes/hours/days/weeks/months/years), not full Intl.RelativeTimeFormat
// precision.
const UNIT_SECONDS: readonly [number, string][] = [
  [31557600, 'year'],
  [2629800, 'month'],
  [604800, 'week'],
  [86400, 'day'],
  [3600, 'hour'],
  [60, 'minute'],
];

@Pipe({ name: 'relativeDate', pure: true })
export class RelativeDatePipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    if (!value) {
      return '';
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return '';
    }

    const seconds = Math.max(0, Math.round((Date.now() - date.getTime()) / 1000));
    if (seconds < 60) {
      return 'just now';
    }

    for (const [unitSize, unitName] of UNIT_SECONDS) {
      if (seconds >= unitSize) {
        const amount = Math.floor(seconds / unitSize);
        return `${amount} ${unitName}${amount === 1 ? '' : 's'} ago`;
      }
    }

    return 'just now';
  }
}
