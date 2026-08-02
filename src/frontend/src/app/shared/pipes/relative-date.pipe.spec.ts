import { RelativeDatePipe } from './relative-date.pipe';

describe('RelativeDatePipe', () => {
  const pipe = new RelativeDatePipe();

  it('returns an empty string for null/undefined', () => {
    expect(pipe.transform(null)).toBe('');
    expect(pipe.transform(undefined)).toBe('');
  });

  it('returns "just now" for a moment a few seconds ago', () => {
    const value = new Date(Date.now() - 5_000).toISOString();
    expect(pipe.transform(value)).toBe('just now');
  });

  it('formats a value a few days ago as "N days ago"', () => {
    const value = new Date(Date.now() - 2 * 86_400_000).toISOString();
    expect(pipe.transform(value)).toBe('2 days ago');
  });

  it('formats a value one hour ago as singular', () => {
    const value = new Date(Date.now() - 3_600_000).toISOString();
    expect(pipe.transform(value)).toBe('1 hour ago');
  });
});
