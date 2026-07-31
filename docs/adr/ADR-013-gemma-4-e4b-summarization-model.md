# ADR-013: Gemma 4 E4B as the Summarization Model

> Status: SUPERSEDED BY ADR-017
> Date: 2026-07-28
> Architecture: docs/architecture.md (v9)

## Context

ADR-001 established local/self-hosted summarization via `IRepositorySummarizer`, and ADR-007
pinned LM Studio as the runtime engine, but neither pinned a specific model to load. F-002 (the
LM Studio throughput spike) and F-008 (the Summarizer implementation) both need a concrete model
target rather than "some open-weight model, TBD."

## Decision

The Summarizer loads the Gemma 4 E4B model in LM Studio.

**A note on verification:** I can confirm Google's Gemma 3n family used "E2B"/"E4B" naming for its
effective-parameter on-device variants as of my training data, but I cannot independently verify a
"Gemma 4" release or confirm this exact model identifier exists in LM Studio's current catalog —
that would postdate what I can check. This decision reflects the operator's explicit instruction;
F-002 must confirm the precise model identifier and quantization actually available in LM Studio
before benchmarking proceeds.

## Alternatives Considered

| Option | Why not chosen |
|--------|-----------------|
| Llama 3.2 (3B/8B) | Comparable on-device-class candidate, but the operator specified Gemma directly; no functional requirement favors Llama specifically. |
| Phi-3.5/Phi-4 mini | Same reasoning — a plausible alternative small model, but not the operator's stated preference. |

## Consequences

- F-002's throughput benchmark (NFR-001, seconds-per-repo target) must be run against this
  specific model, not a generic placeholder — the spike's pass/fail result is only meaningful for
  the model actually pinned here.
- If the exact model identifier turns out to be unavailable in LM Studio's catalog when F-002
  runs, that discovery should produce a new ADR superseding this one with the corrected
  identifier, not a silent substitution.
- Model choice is isolated behind `IRepositorySummarizer` (ADR-001), so swapping models later
  remains a contained change.

**Superseded 2026-08-01 (ADR-017):** F-002's live benchmark confirmed this model passes NFR-001's
throughput target, but also found it spends 65-86% of its output budget on an internal
`reasoning_content` field before the visible summary, truncating output well short of the
requested length — a real problem this ADR's original verification caveat couldn't have
anticipated (it's a behavioral property of the model, not an availability/identifier question).
ADR-017 replaces this pin with Llama 3.2 3B Instruct, chosen from a live comparison against four
already-downloaded alternatives. See ADR-017 for the full decision record.

## Related

- Architecture section: 3. Components → Summarizer; 7. Technology Decisions
- Builds on: ADR-001, ADR-007
- Superseded by: ADR-017 (Llama 3.2 3B Instruct — same reasoning-truncation problem this ADR's own
  Consequences section anticipated as a possible trigger for a superseding ADR, though the actual
  trigger was a behavioral finding, not an availability one)
- Supersedes: none
