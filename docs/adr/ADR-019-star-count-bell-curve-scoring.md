# ADR-019: Star Count Scored on a Bell Curve, Not Monotonically

> Status: ACCEPTED
> Date: 2026-09-06
> Architecture: docs/architecture.md (v30)

## Context

The v1 scoring algorithm treated all five signals the same way: more is better, log-normalized
against a cap so long-tailed counts could not dominate. Star count was given the smallest weight
(10%) and the lowest cap (50 stars) specifically to stop popularity from overwhelming the four
signals the PRD commits to.

That configuration was a workaround for a shape problem, not a solution to it. A monotonic star
curve says the ideal repository is the most-starred one on GitHub. This platform exists to surface
repositories that are _not_ already famous, so the ideal is a middle band — popular enough that
somebody has vouched for the project, not so popular that surfacing it tells the user anything they
could not have found themselves. No weight or cap can express that, because the function is the
wrong shape: it is monotonic where the product need is unimodal.

Capping at 50 stars made the problem worse in practice. Every repository above 50 stars scored
identically on the signal — a 200-star project, a 20,000-star project, and Linux were
indistinguishable. The signal carried almost no information across most of the real population.

Operator direction (2026-09-06) also reweighted the other four signals, moving star count from the
smallest weight to the largest. That only makes sense alongside the shape change: a 50%-weighted
monotonic star signal would turn this into a GitHub popularity ranker.

## Decision

Star count is bucketed into 12 bands and scored on a symmetric Gaussian, replacing the log curve for
this signal only. The other four signals keep the log-normalization unchanged.

Buckets (upper bound inclusive): 100, 250, 500, 1,000, 2,500, 5,000, 10,000, 15,000, 25,000, 50,000,
100,000, and everything above.

The curve is `exp(-(i - 6.5)² / 2σ²)` over bucket index `i ∈ [1,12]`, with σ = 2.5, normalized by its
own maximum so the peak is exactly 1.0 and the component stays in `[0,1]` like every other signal.
Centring at 6.5 — the midpoint of 1..12 — makes buckets 6 and 7 share the peak and the curve exactly
symmetric, so bucket 1 and bucket 12 score identically, as do 2 and 11, and so on.

| Bucket | Stars       | Component | Bucket | Stars          | Component |
| -----: | ----------- | --------: | -----: | -------------- | --------: |
|      1 | 0–100       |    0.0907 |      7 | 5,001–10,000   |    1.0000 |
|      2 | 101–250     |    0.2019 |      8 | 10,001–15,000  |    0.8521 |
|      3 | 251–500     |    0.3829 |      9 | 15,001–25,000  |    0.6188 |
|      4 | 501–1,000   |    0.6188 |     10 | 25,001–50,000  |    0.3829 |
|      5 | 1,001–2,500 |    0.8521 |     11 | 50,001–100,000 |    0.2019 |
|      6 | 2,501–5,000 |    1.0000 |     12 | 100,001+       |    0.0907 |

Weights are simultaneously reset to: star count 50%, contributor count 20%, commits per week 15%,
license present 10%, fork count 5%. They still sum to 1.0 and each remains independently
identifiable per FR-002.

σ = 2.5 is the one free parameter. Symmetry is the requirement; σ only sets how sharply the score
falls away from the middle. At 2.5 the edge buckets retain ~9% of the star component rather than
being zeroed, so a 50-star repository with strong activity can still out-score a dormant one in the
same bucket, while the middle bands stay decisively ahead.

## Alternatives Considered

- **Keep the log curve, raise the cap.** Fixes the "everything above 50 looks identical" problem but
  not the shape one — a 100,000-star repository would still score at or near the maximum.
- **Invert the curve (fewer stars is better).** Would rank every abandoned zero-star repository at
  the top. Star count carries real signal at the low end too: nobody having starred a project is
  evidence, not neutrality.
- **A hard star ceiling as a filter rather than a score.** Excluding everything above N stars is
  cheaper, but it is a cliff: a repository at N+1 disappears entirely rather than scoring slightly
  lower. It also moves a scoring concern into the crawler's discovery query.
- **Continuous log-space Gaussian instead of discrete buckets.** Mathematically tidier and avoids
  boundary cliffs within the curve. Rejected for explainability: the buckets are the thing an
  operator reasons about and the UI can show, and the 12 bands were specified directly.

## Consequences

- Every existing `Score` row is now computed under different rules. Scores are append-only history,
  so nothing is rewritten; the next crawl produces new rows under the new algorithm and the
  dashboard's per-repository trend growth will show a one-off step change across that boundary. This
  is cosmetic and self-correcting after one cycle.
- Ranking changes substantially. Repositories in the 2,500–10,000 star band rise; both the
  near-zero-star and the very-famous tails fall.
- The signal is no longer monotonic, so "sort by stars" and "sort by score" now genuinely diverge —
  which is the point, but it means a test asserting "more stars scores higher" is no longer a valid
  invariant. Replaced with symmetry, peak-position, and rise-then-fall assertions.
- Bucket boundaries are a product decision now encoded in one array. Changing them changes every
  score, so they belong in an ADR rather than only in code comments.
- Weight percentages shown in the digest email's "How We Score" panel are read from the
  `ScoringWeights` constants, so they track this change automatically.

## Related

- Supersedes the star-count portion of the original five-signal weighting (changelog revision for
  F-008 / the mid-flight star-count addition). The other four signals' log-normalization is
  unchanged.
- Fixes review finding L-6 in `docs/code-review.md` in the same pass: a contributor count GitHub
  refuses to enumerate ("too large to list contributors") now scores full marks for that signal
  rather than zero. That case only arises for repositories with very large contributor bases, so
  scoring it as zero inverted the signal and, at the new 20% weight, docked the repositories with
  the healthiest communities.
- FR-002 (weighted signals) and the PRD's "not stars alone" constraint are both still satisfied —
  more strongly than before, since star count can no longer be maximized by being popular.
