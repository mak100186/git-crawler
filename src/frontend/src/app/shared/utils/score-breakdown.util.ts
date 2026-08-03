import { ScoreBreakdownDto } from '../../core/models/repository.model';

export interface ScoreRow {
  label: string;
  value: string;
  weight: number;
  ratio: number; // 0..1, drives the progress bar width
}

// The five caps ScoringWeights.cs log-normalizes each raw signal against (backend, ComputeScores) -
// mirrored here purely to draw an accurate "how much of this signal's ceiling did this repo hit"
// progress bar per signal row (FR-005's "independently-weighted, identifiable inputs"). This does
// not duplicate any scoring *decision* - the actual score always comes from the API's totalScore -
// it only reproduces the same display-normalization math the design's per-signal bars need. Shared
// between RepositoryCard's "Why this score?" panel and RepositoryDetailPane's score breakdown so the
// two displays can't drift apart.
const COMMITS_PER_WEEK_CAP = 10.0;
const CONTRIBUTOR_COUNT_CAP = 25.0;
const FORK_COUNT_CAP = 200.0;
const STAR_COUNT_CAP = 50.0;

function normalizeLog(rawValue: number, cap: number): number {
  if (rawValue <= 0) {
    return 0;
  }
  return Math.min(Math.log(rawValue + 1) / Math.log(cap + 1), 1);
}

export function buildScoreRows(breakdown: ScoreBreakdownDto): ScoreRow[] {
  return [
    {
      label: 'License',
      value: breakdown.hasLicense ? (breakdown.licenseType ?? 'Licensed') : 'None',
      weight: breakdown.licenseWeight,
      ratio: breakdown.hasLicense ? 1 : 0,
    },
    {
      label: 'Commits/week',
      value: breakdown.commitsPerWeek.toFixed(1),
      weight: breakdown.commitsPerWeekWeight,
      ratio: normalizeLog(breakdown.commitsPerWeek, COMMITS_PER_WEEK_CAP),
    },
    {
      label: 'Contributors',
      value: String(breakdown.contributorCount),
      weight: breakdown.contributorCountWeight,
      ratio: normalizeLog(breakdown.contributorCount, CONTRIBUTOR_COUNT_CAP),
    },
    {
      label: 'Forks',
      value: String(breakdown.forkCount),
      weight: breakdown.forkCountWeight,
      ratio: normalizeLog(breakdown.forkCount, FORK_COUNT_CAP),
    },
    {
      label: 'Stars',
      value: String(breakdown.starCount),
      weight: breakdown.starCountWeight,
      ratio: normalizeLog(breakdown.starCount, STAR_COUNT_CAP),
    },
  ];
}
