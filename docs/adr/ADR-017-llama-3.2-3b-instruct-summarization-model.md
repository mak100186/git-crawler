# ADR-017: Llama 3.2 3B Instruct Replaces Gemma 4 E4B as the Summarization Model

> Status: ACCEPTED
> Date: 2026-08-01
> Architecture: docs/architecture.md (v12)

## Context

ADR-013 pinned Gemma 4 E4B as the summarization model, per the operator's explicit preference at
the time, with an open caveat that its exact identifier/behavior wasn't independently verifiable.
F-002's live benchmark (`docs/spikes/f-002-lm-studio-throughput-benchmark.md` §9) confirmed
`google/gemma-4-e4b` exists, is available, and passes NFR-001's throughput target (2.57-2.82s p95
per repo) — but also found a real, unanticipated problem: the model spends 65-86% of its
`max_tokens: 300` output budget on an internal `reasoning_content` field before the visible
summary, truncating every single test response to 30-60 words against a "under 150 words" target.
This is not a speed problem (throughput passed easily); it's an output-completeness problem
specific to this model's reasoning behavior.

Rather than work around it by only raising `max_tokens` (§9.4's first mitigation option — treats
the symptom, still burns most of the budget on invisible reasoning, and its wall-clock cost at a
wider budget was unverified), the operator asked whether another already-downloaded model would
avoid the problem entirely. Four alternatives already present in LM Studio's catalog
(`gemma-3-4b-it`, `gemma-4-12b-it-qat`, `llama-3.2-3b-instruct`, `qwen2.5-coder-7b-instruct`) were
tested live against the identical request (same README, same system prompt, same `max_tokens: 300`
cap) that had produced the truncated `gemma-4-e4b` output.

## Decision

The Summarizer loads **Llama 3.2 3B Instruct** (`llama-3.2-3b-instruct` in LM Studio's catalog),
replacing Gemma 4 E4B.

**Live comparison data (2026-08-01), single mid-size request, identical `max_tokens: 300` cap:**

| Model | Wall time | `finish_reason` | Visible output | Reasoning tokens |
|---|---|---|---|---|
| `google/gemma-4-e4b` (ADR-013, superseded) | 2.57-2.82s (p95) | `length` (truncated) | 30-60 words | 195-258 of 300 (65-86% wasted) |
| `gemma-3-4b-it` | 1.33s | `stop` (complete) | ~120 words | 0 |
| `gemma-4-12b-it-qat` | 2.89s | `stop` (complete) | ~125 words | 0 |
| **`llama-3.2-3b-instruct` (chosen)** | **0.88s** | `stop` (complete) | ~90 words | **0** |
| `qwen2.5-coder-7b-instruct` | 1.60s | `stop` (complete) | ~120 words | 0 |

Notably, `gemma-4-12b-it-qat` shares Gemma 4 E4B's `gemma4` architecture family but produces
**zero** reasoning-token overhead — confirming the problem is specific to the `e4b` fine-tune, not
Gemma 4 broadly. This rules out "avoid Gemma entirely" as the actual lesson here; it's a
model-specific finding, not an architecture-family one.

**Full-rigor benchmark for the chosen model** (n=10 per README size, matching F-002 §9.2's
methodology exactly — see `docs/spikes/f-002-lm-studio-throughput-benchmark.md` §10 for full data):

| README size | n | mean | p50 | p95 | max |
|---|---|---|---|---|---|
| small | 10 | 0.866s | 0.876s | 0.976s | 0.976s |
| mid | 10 | 0.777s | 0.777s | 0.829s | 0.829s |
| large | 10 | 1.050s | 0.992s | 1.544s | 1.544s |

Native stats (mid, single call): 241.6 tok/s, `stop_reason: "eosFound"` (natural completion, not
budget-capped), 165 of 300 completion tokens used — 45% headroom left in the budget, not zero.

## Alternatives Considered

| Option | Why not chosen |
|--------|-----------------|
| Keep `gemma-4-e4b`, raise `max_tokens` (F-002 spike §9.4's first mitigation) | Treats the symptom, not the cause — still burns the majority of every response's budget on invisible reasoning; the wall-clock cost at a wider budget was unverified and would need its own re-benchmark; doesn't fix the underlying waste. |
| `gemma-4-12b-it-qat` | Genuinely viable — same family as the original ADR-013 pin, complete output, zero reasoning waste, still comfortably passes NFR-001 (2.89s). Not chosen only because `llama-3.2-3b-instruct` is ~3x faster with equally complete output and no functional requirement favors staying within the Gemma family specifically (ADR-013's original "operator preference" rationale for Gemma no longer applies once Gemma's own e4b variant is what caused the problem). Worth reconsidering if `llama-3.2-3b-instruct`'s summary quality proves inadequate in practice. |
| `gemma-3-4b-it` | Also viable, complete output, close second on speed (1.33s). Not chosen — `llama-3.2-3b-instruct` measured faster with no observed quality tradeoff in this single-request comparison; a closer call than `gemma-4-12b-it-qat` and worth revisiting if `llama-3.2-3b-instruct`'s output quality underperforms at scale. |
| `qwen2.5-coder-7b-instruct` | Coder-specialized model; repository summarization isn't a code-generation task, so a general instruct model is a better fit — included in the comparison for completeness, not because it was a strong candidate. |
| `deepseek/deepseek-r1-0528-qwen3-8b` | Not tested — "R1" branding is a well-established, explicit reasoning-model designator (chain-of-thought by design), so it would predictably make the exact problem being solved worse, not better. Excluded on that basis rather than spending a live test on a near-certain negative result. |

## Consequences

- `IRepositorySummarizer` (ADR-001) already isolates the rest of the system from the model choice
  — this is a contained config change, not a code-architecture change, and F-008 (not yet
  implemented) is unaffected beyond its config default.
- `Makefile`'s `LMSTUDIO_MODEL` default, `.env.example`, and `docs/setup.md`'s prerequisites all
  need updating to `llama-3.2-3b-instruct` — done as part of this ADR's rollout, not deferred.
- PM-005 (`docs/project-management.md`) — originally tracking "F-008 must address the
  reasoning-token truncation finding" — is closed by this model swap rather than by the
  `max_tokens`-increase mitigation it was written around; F-008 still shouldn't blindly reuse
  `max_tokens: 300` without checking it's adequate for `llama-3.2-3b-instruct`'s own output length
  needs (165/300 tokens used in the mid-size test — comfortable but not unlimited headroom),
  though there's no known truncation risk at that setting for this model.
- Architecture risk A2 (`docs/architecture.md` §8) remains Resolved — the specific model changed,
  but the resolution (throughput passes NFR-001 with wide margin) holds even more strongly for the
  new pick (0.78-1.05s mean vs. `gemma-4-e4b`'s 2.57-2.82s).
- `gemma-4-e4b` remains downloaded in LM Studio's catalog (not deleted) — no cleanup action taken
  or required; this ADR only changes what the platform's config defaults to loading.

## Related

- Architecture section: 3. Components → Summarizer; 7. Technology Decisions; 8. Open Questions & Risks (A2)
- Builds on: ADR-001, ADR-007, ADR-016 (LM Studio deployment topology — unaffected by this change)
- Supersedes: ADR-013 (Gemma 4 E4B summarization model pin)
