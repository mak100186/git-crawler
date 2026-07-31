# Spike F-002: LM Studio Inference Throughput Benchmark (Gemma 4 E4B → Llama 3.2 3B Instruct)

> Status: **COMPLETE — final model pick is Llama 3.2 3B Instruct, not Gemma 4 E4B. See §10.**
> §3's methodology was run for real against `google/gemma-4-e4b` (§9), which passed NFR-001's
> throughput target but truncated output due to reasoning-token overhead. §10 documents a live
> comparison against four alternatives and the resulting model swap (ADR-017 supersedes ADR-013).
> The §9 measurements remain in this document as real historical data — they are not wrong, the
> model they were measured against is simply no longer the one this platform uses.
> Date: 2026-07-31 (original spike), §9 measured results added 2026-08-01, §10 model swap 2026-08-01
> Feature: F-002 (Phase 0)
> Resolves: Architecture risk A2 (docs/architecture.md §8) — see §9.5 and §10 for the precise scope of what's resolved
> Related: ADR-001, ADR-007, ADR-013 (superseded), ADR-016, ADR-017, NFR-001 (docs/architecture.md §6)

## 0. Confidence Disclaimer

This spike was produced without the ability to call a live LM Studio instance, load a model, or
measure a single token of real inference. Every number below that looks like a benchmark result is
either **(estimated, needs live verification)** or explicitly marked as not measured. Nothing in
this document should be read as "F-002 benchmarked Gemma 4 E4B and it passed/failed" — that
determination has not happened yet. What this document *does* provide, per the Task Packet's own
framing of the correct output for a no-access environment:

1. The most honest, best-available assessment of Gemma 4 E4B's existence/identifier/availability
   (§2), including the parts of that assessment I cannot verify.
2. A concrete, runnable benchmark methodology (§3) the operator can execute against a real LM
   Studio instance with no further design work required.
3. An estimation framework (§4) that brackets plausible throughput by hardware class, using
   general, high-confidence knowledge about local LLM inference on llama.cpp-class runtimes — not
   a fabricated single number.
4. An explicit NFR-001 comparison framework (§5) so that whoever runs §3's plan has a predefined
   pass/marginal/fail bar to apply to the numbers they get, instead of an ad hoc judgment call.
5. Explicit handling of the "unavailable or underperforming" edge case (§6), and a verdict on risk
   A2 that says what it can and cannot say (§7).

## 1. Assumptions Stated Up Front

1. **"LM Studio instance" means a local install exposing its OpenAI-compatible REST API** on
   `http://localhost:1234` (LM Studio's documented default port as of the versions I have
   knowledge of), not a remote/hosted LM Studio deployment. Architecture §3 (Summarizer) and ADR-002
   (Docker Compose, self-hosted) both frame this as co-located infrastructure, so this is the
   natural reading, not a stretch.
2. **Operator hardware is unspecified.** Neither the PRD, Architecture doc, nor any ADR states what
   machine LM Studio will run on (CPU-only, consumer GPU, Apple Silicon, or a dedicated inference
   box). This is a real gap, not a nit — local LLM throughput is dominated by hardware class far
   more than by which ~4B-class model is loaded, so any single-number estimate would be more
   fabrication than analysis. §4 brackets by hardware tier instead of assuming one.
3. **"Representative repository README (~1-3 KB of content)"** is taken directly from TC-002-02;
   §3's methodology treats input size as a controlled variable (three README sizes, not one) so the
   benchmark isn't overfit to a single content length.
4. **"On the order of seconds per repository" (NFR-001)** is not a pinned numeric SLA anywhere in
   the Architecture doc or PRD. §5 proposes a concrete interpretation (order-of-magnitude bands)
   since the benchmark methodology needs *some* threshold to compare against — this is a stated,
   reasoned assumption, not an authoritative number, and the Architecture owner should confirm or
   adjust it.
5. **Summarization volume is a subset of discovery volume.** FR-003 scopes summarization to
   "top-scored repositories," not the full 1,000-5,000/day discovery volume from F-001. Neither the
   PRD nor Architecture doc states what fraction of discovered repos clear the scoring threshold, so
   this spike cannot translate "seconds per repo" into "total Summarizer stage duration per day"
   with any confidence — flagged as an open unknown in §8, not guessed at.

## 2. Model Identifier & Availability (TC-002-01)

**Cannot be confirmed from this environment — must be verified live by the operator.** Here is
what can and cannot be said with the confidence level attached to each claim:

| Claim | Confidence |
|---|---|
| Google's Gemma model family exists, is open-weight, and has previously used effective-parameter on-device variant naming | **(documented, high confidence)** — consistent with what ADR-013 itself already states about "Gemma 3n" using "E2B"/"E4B" naming for on-device variants as of my training data. |
| A "Gemma 4" release (as opposed to Gemma 3n, or a Gemma 3.x point release) exists | **Cannot verify.** ADR-013 already flags this exact gap: it postdates what I can confirm from training data. I have no basis to either confirm or deny a "Gemma 4" release exists, and I will not guess. |
| "Gemma 4 E4B" is the exact catalog/file identifier LM Studio would show (e.g. a specific Hugging Face repo + GGUF quantization tag such as `Q4_K_M`, `Q8_0`, `MLX-4bit`) | **Cannot verify.** LM Studio's catalog is a live, changing surface (it pulls GGUF/MLX conversions primarily from Hugging Face, commonly including the `lmstudio-community` curation org for fast-turnaround quantizations of newly released models). Even if "Gemma 4" exists, I have no way to confirm what identifier string LM Studio's search would return for it, what quantization options exist, or what context-window/resource footprint it ships with. |
| If Gemma 4 does not exist or isn't yet available as a GGUF/MLX conversion, the closest real analog I can identify is the Gemma 3n E2B/E4B family | **(reasoned inference, not verification)** — offered only as the fallback path in §6, not as a substitute decision; ADR-013 explicitly reserves that call for a superseding ADR, not this spike. |

**Concrete verification steps for the operator** (none of these require code, only LM Studio itself
or a browser):

1. Open LM Studio → the "Discover" / search tab → search `gemma 4 e4b`. Record whatever result set
   comes back verbatim (exact repo name, quantization variants offered, file size, context length).
2. If nothing matches, search `gemma 3n e4b` as the fallback check, per the reasoned-inference row
   above — this tells you whether the naming pattern ADR-013 anticipated exists under a different
   version number.
3. Cross-check via Hugging Face directly (no LM Studio needed): browse
   `https://huggingface.co/lmstudio-community` and `https://huggingface.co/google` for any Gemma 4
   or Gemma 3n E4B GGUF repo. The `lmstudio-community` org is LM Studio's own curated
   quantization feed **(documented, high confidence this org exists and serves this purpose as of
   my training data — confirm the URL still resolves before relying on it)**.
4. Once a candidate model is downloaded and loaded in LM Studio, confirm the server-visible
   identifier with:
   ```bash
   curl -s http://localhost:1234/v1/models | jq .
   ```
   The `id` field returned here — not the display name in the GUI — is the exact string to record
   as "the confirmed identifier" and to use as the `"model"` value in every request in §3.

**Explicit statement required by TC-002-01:** this spike does not claim Gemma 4 E4B is confirmed
available. It states plainly that availability is unverifiable from this environment and specifies
exactly how the operator confirms it in under five minutes. If step 1/2 above turn up nothing, that
is itself the answer to TC-002-01 ("explicitly unavailable"), and §6/§7 apply.

**Addendum, 2026-08-01 — availability now confirmed live, on the actual target machine.** Unlike
the rest of this document, this was verified directly, not estimated. The operator's LM Studio
install (the same one this platform will run against per ADR-016) has `google/gemma-4-e4b` already
downloaded:

```
$ lms ls
LLM                                               PARAMS    ARCH      SIZE       DEVICE
...
google/gemma-4-e4b (1 variant)                    7.5B      gemma4    6.33 GB    Local
...
$ lms load google/gemma-4-e4b --identifier gitcrawler-summarizer --context-length 8192 --gpu max -y
Model loaded successfully in 6.21s.
(5.89 GiB)
```

This closes TC-002-01/the first half of AC1 unconditionally — the identifier is `google/gemma-4-e4b`,
it exists, it's a real 7.5B-parameter model on the `gemma4` architecture, and it loads successfully.
**This does not close the rest of F-002 or resolve risk A2**: model *load* time (6.21s, one-time,
cold-start) is a different measurement from *inference throughput per repository summary*
(tokens/sec against real README content, which is what NFR-001 and §3-§5 of this document actually
require). The full runnable benchmark in §3 still needs to be executed against a loaded model with
real prompts to produce a throughput number comparable to NFR-001's bands in §5. See
`docs/project-management.md` PM-004.

## 3. Benchmark Methodology — Runnable Plan (TC-002-02, TC-002-04)

### 3.1 Prerequisites

- LM Studio installed, the target model downloaded and loaded (§2), and the local server started
  (LM Studio → Developer tab → "Start Server," default `http://localhost:1234`; the `lms` CLI's
  `lms server start` and `lms load <identifier>` are documented equivalents in recent LM Studio
  versions, but confirm exact flags with `lms load --help` against the installed version rather
  than trusting this document's flag names — CLI flags are exactly the kind of detail that drifts
  across versions).
- `curl` and `jq` available in the operator's shell (both are already assumed by this repo's other
  spike, F-001, for its own runnable snippets, so this is a consistent tooling choice).
- Three real README files pulled from the actual repository corpus this platform will summarize,
  saved locally, sized to bracket TC-002-02's "~1-3 KB" range: `readme-small.md` (~1 KB),
  `readme-mid.md` (~2 KB), `readme-large.md` (~3 KB). Using real content (not lorem ipsum) matters
  because tokenization density varies with real prose/code-block mix.

### 3.2 Confirm server and model

```bash
curl -s http://localhost:1234/v1/models | jq .
```
Record the exact `id` string from the response — this is what confirmed TC-002-01, and it is the
`"model"` value every request below must use.

### 3.3 Build a request body from real README content (avoids shell-quoting corruption of multi-KB text)

```bash
jq -n --rawfile readme ./readme-mid.md \
  --arg model "REPLACE_WITH_EXACT_ID_FROM_3.2" \
  '{
    model: $model,
    messages: [
      {role: "system", content: "You are a repository summarizer. Produce a structured summary covering: purpose, key features, tech stack, and notable caveats, in under 150 words."},
      {role: "user", content: $readme}
    ],
    temperature: 0.2,
    max_tokens: 300,
    stream: false
  }' > request-mid.json
```
Repeat for `readme-small.md` → `request-small.json` and `readme-large.md` → `request-large.json`.
A fixed, realistic system prompt matters — it should match (or closely approximate) the actual
prompt F-008's `IRepositorySummarizer` implementation will use, since prompt length itself affects
input-token count and therefore latency.

### 3.4 Single timed run (wall-clock, per TC-002-02's explicit "measure wall-clock time" instruction)

```bash
curl -s -w "\ntotal_time_s: %{time_total}\n" \
  -H "Content-Type: application/json" \
  -d @request-mid.json \
  http://localhost:1234/v1/chat/completions -o response-mid.json
```
`curl`'s `%{time_total}` measures true wall-clock time from request start to full response receipt
— exactly what TC-002-02 asks for, no separate timing harness needed.

### 3.5 Multi-run loop (TC-002-04 repeatability)

TC-002-04's stated minimum is 3 runs. **3 samples is enough to satisfy the letter of TC-002-04, but
is too small to estimate a meaningful p95** — 3 points can't characterize a tail distribution. This
spike recommends **10 runs minimum per README size** (30 total across the three sizes) as the
practical bar for the p95 comparison in §5, with 3 as an absolute floor only if time is constrained:

```bash
for size in small mid large; do
  echo "=== $size ===" | tee -a benchmark-results.txt
  for i in $(seq 1 10); do
    curl -s -w "run=$i size=$size total_time_s=%{time_total}\n" \
      -H "Content-Type: application/json" \
      -d @"request-$size.json" \
      http://localhost:1234/v1/chat/completions -o /dev/null
  done | tee -a benchmark-results.txt
done
```

### 3.6 Optional: token-level stats, if the installed LM Studio version exposes them

LM Studio has, in versions I have knowledge of, offered a native (non-OpenAI-compatible) endpoint
at `/api/v0/chat/completions` that echoes back a `stats` object with fields like
`tokens_per_second` and `time_to_first_token` **(documented in earlier LM Studio releases per my
training knowledge — treat the exact path and field names as needing confirmation against the
installed version's own docs/OpenAPI page before relying on them; this is exactly the kind of
detail that changes across releases)**:

```bash
curl -s -H "Content-Type: application/json" -d @request-mid.json \
  http://localhost:1234/api/v0/chat/completions | jq '.stats // "not available in this LM Studio version"'
```
If present, this gives tokens/sec directly instead of having to back-calculate it from wall-clock
time and output length — useful for diagnosing *why* a run is slow (prompt processing vs.
generation) but not required; §3.4/§3.5's wall-clock measurement is the one TC-002-02 actually asks
for and works regardless of LM Studio version.

### 3.7 Compute summary statistics

```bash
grep 'size=mid' benchmark-results.txt | grep -oP 'total_time_s=\K[0-9.]+' | sort -n | awk '
  { a[NR]=$1; sum+=$1 }
  END {
    n=NR
    mean=sum/n
    p50=a[int(n*0.5)+1]
    p95=a[int(n*0.95)+1]
    printf "n=%d mean=%.2fs p50=%.2fs p95=%.2fs max=%.2fs\n", n, mean, p50, p95, a[n]
  }'
```
Repeat per README size. Report mean, p50, p95, and max — not just mean — since a fast mean can hide
exactly the tail-latency problem TC-002-04 exists to catch.

**Note on p95 at n=10:** nearest-rank p95 only diverges from the maximum once the sample has at
least 20 points (`int(n*0.95)+1` reaches the last index for any `n<20`) — so at this section's
recommended n=10, the `p95=` value printed above will always equal `max=`; it is not yet a distinct
tail-percentile measurement, just the sample max restated. If a p95 genuinely distinct from the max
matters for the §5 NFR-001 comparison, increase to n≥20 runs per README size. If time-constrained and
staying at n=10, treat `p95` and `max` as the same number and read §5's p95 bands as "max observed
time at 10 samples," not a true 95th-percentile estimate.

### 3.8 Cold-load vs. warm-state distinction

Run the very first request immediately after loading the model, timed separately from the §3.5
loop, and label it explicitly. If the Summarizer stage (F-008) unloads the model between scheduled
Hangfire runs (Architecture §3 Job Scheduler), first-call latency after each run's model load is the
number that matters for NFR-001, not steady-state warm latency — the two can differ substantially
for local LLM runtimes (model weights loading into VRAM/RAM is a one-time cost per load, separate
from per-request inference cost). Report both; do not average them together.

## 4. Estimation Framework — Projected Throughput (No Live Access)

Since no live run happened in this environment, this section brackets plausible outcomes using
general, high-confidence knowledge of local LLM inference behavior on llama.cpp/GGUF-class runtimes
(which LM Studio is built on) — **not** knowledge specific to Gemma 4 E4B, which per §2 cannot be
verified to exist. Treat this table as "what a ~4B-effective-parameter, GGUF-quantized model
plausibly does on comparable hardware," explicitly extrapolated, not measured:

Assumed workload: ~500-750 input tokens (a ~1-3 KB README plus a short system prompt) + ~200-300
output tokens (a concise structured summary, per the `max_tokens: 300` cap in §3.3).

| Hardware tier | Typical generation throughput (tok/s) — general local-LLM knowledge, ~4-8B GGUF Q4-class | Projected total time/repo (est.) | Basis |
|---|---|---|---|
| Consumer GPU, 8-12+ GB VRAM (e.g. RTX 3060/4060/4070 class) | ~30-80 tok/s | **~3-12 seconds** | **(estimated, needs live verification)** — broad, well-established range for ~4-8B Q4-quantized models on mid-range consumer GPUs with full GPU offload; prompt processing for 500-750 tokens is typically sub-second on GPU and not the dominant cost. |
| Apple Silicon (M-series, Metal/MLX) | ~20-60 tok/s | **~4-15 seconds** | **(estimated, needs live verification)** — comparable range to consumer GPU tier for similarly sized quantized models, per general knowledge of Metal/MLX-backed llama.cpp inference. |
| CPU-only, modern multi-core desktop, no GPU offload | ~3-15 tok/s | **~15-100+ seconds** | **(estimated, needs live verification)** — CPU-only inference for models in this parameter class is commonly an order of magnitude slower than GPU-offloaded inference; prompt processing also becomes a non-trivial fraction of total time in this tier, unlike the GPU tiers. |

One additional, lower-confidence factor specific to the Gemma 3n E2B/E4B *lineage* (offered only as
context, given §2's finding that "Gemma 4" itself is unverifiable): Google's stated design goal for
that naming pattern was reduced active-parameter/memory footprint via architecture tricks
(MatFormer-style nested sub-models, per-layer embedding caching) specifically to improve on-device
throughput relative to a naively-sized dense model of similar quality — **if a "Gemma 4 E4B" model
inherits that design goal, actual throughput could sit toward the faster end of its tier rather
than the slower end. This is the single weakest-confidence claim in this document** — it rests on
extrapolating a pre-cutoff architectural pattern to a model release this spike has already stated
it cannot confirm exists, and should not move the verdict in §7 on its own.

**What this table is for:** giving the operator a sanity-check range to compare their actual §3
results against, and flagging that the CPU-only tier is where NFR-001 risk concentrates — if the
operator's hardware is CPU-only, the estimation framework itself predicts a meaningful chance of
failing the "seconds" order-of-magnitude bar, which should raise the priority of actually running
§3 before F-008 begins, not lower it.

## 5. NFR-001 Comparison Framework (TC-002-02)

NFR-001 states summary generation should complete "on the order of seconds per repository" — an
order-of-magnitude target, not a pinned SLA number. This spike proposes the following concrete bands
so that §3's results (once run) have a predefined bar to compare against, rather than an ad hoc
judgment call at benchmark time. **This banding is this spike's own reasoned interpretation of a
fuzzy requirement, not an authoritative reinterpretation of NFR-001** — the Architecture owner
should confirm or adjust these thresholds if a tighter number is intended:

| Band | p95 wall-clock time per repo (from §3.7) | Verdict |
|---|---|---|
| Pass | ≤ ~30 seconds | Squarely "on the order of seconds"; no action needed. |
| Marginal | ~30 seconds - 2 minutes | Order-of-magnitude has drifted from "seconds" toward "a couple minutes." Not an automatic fail, but should trigger the mitigation options in §6 before F-008 sign-off, not be waved through silently. |
| Fail | > 2 minutes p95, or mean/median already exceeds the "seconds" order of magnitude | Fails NFR-001 as stated. §6 applies. |

Additional check beyond the raw per-repo number: **compare mean vs. p95** from §3.7. If p95 is more
than ~3x the mean, that is itself a finding worth reporting even within the "Pass" band — it means
occasional runs run long enough to matter for a scheduled batch job (Architecture §3 Job Scheduler
processes multiple top-scored repos per run), even if the typical case looks fine. This is exactly
the tail-latency risk TC-002-04 was written to catch, and a mean-only comparison would miss it.

Also note the §1.5 caveat: this framework evaluates *per-repo* time only, because neither the PRD
nor Architecture doc states how many repos clear the scoring threshold per day. A per-repo pass does
not automatically mean the full Summarizer stage duration is acceptable — that requires the
top-scored-repo daily count, which is an open unknown (§8), not something this spike can compute.

## 6. Edge Case Handling — Model Unavailable or Underperforming (TC-002-03)

Explicit, not silent, per TC-002-03 and the Task Packet's AC4:

**If §2 finds the model unavailable** (neither "Gemma 4 E4B" nor "Gemma 3n E4B" resolves to
anything in LM Studio's catalog or on Hugging Face):
1. Do not silently substitute a different model and proceed as if ADR-013 were unaffected.
2. Per ADR-013's own Consequences section (already anticipates this exact case): file a new ADR
   superseding ADR-013 with the corrected model identifier, before F-008 (Summarizer) begins
   implementation against it.
3. Candidate fallbacks to evaluate in that superseding ADR, in order of closeness to the original
   intent: (a) Gemma 3n E4B if it exists and resolves (closest to ADR-013's stated naming pattern),
   (b) Gemma 3n E2B (smaller/faster, explicit quality tradeoff), (c) the alternatives ADR-013 itself
   already rejected only because "the operator specified Gemma directly" (Llama 3.2 3B/8B, Phi-3.5/
   Phi-4 mini) — worth reconsidering once the constraint that ruled them out no longer holds.

**If §3's measured results land in the "Fail" band of §5** (or "Marginal" with no acceptable
mitigation):
1. Do not silently accept a result that misses NFR-001 and move on to F-008 unchanged.
2. Mitigation options to try, roughly in order of effort:
   - Reduce `max_tokens` (shorter structured summary) — directly cuts generation time, the dominant
     cost per §4.
   - Truncate/pre-filter README input beyond a fixed length cap — reduces prompt-processing cost,
     smaller effect than output-length reduction per §4's reasoning but non-zero, especially on the
     CPU-only tier.
   - Try a smaller quantization (e.g. move from Q8/Q6 to Q4) if the currently loaded quantization
     isn't already the smallest reasonable option — direct speed/quality tradeoff, should be
     evaluated against summary quality, not applied blindly.
   - If hardware is CPU-only per §4, evaluate whether GPU offload is available/affordable — the
     single largest lever per §4's tier spread.
   - Fall back to the smaller Gemma variant (E2B) if the E4B tier specifically is what's failing.
3. **If none of the above closes the gap:** escalate — flag NFR-001 itself for the Architecture
   owner to revisit (is "seconds per repository" the right target given real hardware constraints,
   or should it be relaxed for a solo self-hosted deployment), rather than either quietly missing it
   in production or blocking indefinitely on an unreachable target. This is the explicit
   "operator/Architecture-owner decision point" the Task Packet's AC4 calls for.

## 7. Resolution Verdict on Risk A2

> **Superseded by live measurement — see §9 for full detail.** This table originally recorded the
> pre-execution verdict (both rows "Not resolved," written 2026-07-31 with no live access). Updated
> 2026-08-01 to reflect §9's actual results rather than leaving a stale "not resolved" verdict
> sitting above a "resolved" one below it in the same document.

| Scope | Verdict |
|---|---|
| Model identifier/availability (TC-002-01) | **Resolved 2026-08-01.** `google/gemma-4-e4b` confirmed live via `lms ls`/`lms load`/`/v1/models` against the operator's actual install (§2 addendum). |
| Throughput vs. NFR-001 (TC-002-02, TC-002-04) | **Resolved 2026-08-01, as measured at `max_tokens: 300`.** §9.2: 2.57-2.82s p95 across all three README sizes — ~10x headroom under §5's 30s Pass threshold. See §9.5 for the one caveat (re-verify once `max_tokens` is widened per §9.4). |
| Overall risk A2 (Architecture §8) | **Resolved.** Both sub-questions above are now answered from real measurements, not estimates. §9.4 surfaces a separate, non-throughput finding (reasoning-token budget truncation) that needs action before F-008 — tracked as a new follow-up, not a reason to reopen A2. |

**Explicit statement required by the Task Packet's AC4, updated:** ADR-013 and NFR-001 are no
longer "provisionally accepted pending verification" — both are now validated by real measurement
(§9). The one open item is operational, not a validation gap: F-008 must not copy §3.3's
`max_tokens: 300` verbatim without addressing §9.4 first.

## 8. Follow-Ups (Not in Scope for This Spike)

1. ~~Operator executes §3 against a real LM Studio instance and real hardware, and records the raw
   results~~ **Done 2026-08-01 — see §9.** This was the single most important follow-up; everything
   else here was downstream of it.
2. ~~If §2's live check finds the model unavailable: file the ADR superseding ADR-013~~ **Not
   needed — §2's addendum confirms the model is available.**
3. ~~If §3/§5 finds throughput in the Marginal or Fail band with no acceptable §6 mitigation: raise
   the NFR-001 revisit~~ **Not needed — §9.6 verdict is Pass with wide margin.**
4. ~~Architecture doc's own risk register (§8, row A2) update to reflect this spike's verdict~~
   **Still the orchestrator/human's job, not this spike's** — same pattern as F-001's spike. Now
   actionable: mark A2 "Resolved" per §7/§9.
5. **New, from §9.4 — not anticipated when this follow-up list was first written:** F-008's
   `IRepositorySummarizer` implementation must not copy §3.3's `max_tokens: 300` verbatim — it
   truncates `google/gemma-4-e4b`'s visible output to 30-60 words due to reasoning-token overhead.
   See §9.4 for the three mitigation options to evaluate during F-008 implementation.
6. **Still open, not computed here:** what fraction of daily discovered repos clear the scoring
   threshold and require summarization (FR-003's "top-scored repositories"). Needed to convert a
   validated per-repo time (§9.6) into a total Summarizer-stage duration budget per scheduled run —
   currently absent from the PRD and Architecture doc. Worth raising alongside PM-001/PM-002 in the
   PMBook's open items if it isn't pinned down before F-008.

## 9. Measured Results (2026-08-01) — §3 Actually Executed

Everything below is a real measurement against the operator's actual machine, not an estimate.
Environment: `google/gemma-4-e4b` loaded via `lms load ... --context-length 8192 --gpu max`,
identifier `gitcrawler-summarizer` (confirmed server-visible per §2's addendum). Hardware tier
unknown/unstated by the operator, but the throughput observed (§9.2) is consistent with the
"Consumer GPU" or "Apple Silicon" tiers in §4's bracket, not CPU-only.

**Tooling substitution, disclosed per this project's honesty norm:** §3.3 specifies `jq` for
building request bodies; `jq` was not installed in the execution environment. Used Python's `json`
module instead (equivalent behavior — safe escaping of multi-KB text, no shell-quoting risk) to
build `request-{small,mid,large}.json`. `curl` was available as specified and used exactly as
written for all timing.

**Fixtures used, real content (not lorem ipsum), per §3.1:** `src/frontend/README.md` (1,462
bytes, small), `node_modules/es-define-property/README.md` (2,056 bytes, mid), `node_modules/rxjs/README.md`
(3,834 bytes, large — a real, unmodified npm package README already present in the repo tree,
substituted for a hypothetical "real repository corpus" fixture since no live crawler exists yet
to source one; large tier runs slightly past the "~1-3 KB" range stated in §3.1, kept as-is rather
than truncated, since it's still representative real-world README length).

### 9.1 Cold-load vs. warm-state (§3.8)

- Model load time (one-time, per `lms load`): **4.90s - 6.21s** across two separate loads this
  session (§2's addendum recorded 6.21s; this run's fresh load was 4.90s) — some run-to-run
  variance in load time itself, not just inference.
- First request immediately after load (`mid` size): **3.16s** wall-clock.
- Subsequent warm-state `mid` requests (§9.2): mean **2.57s**.
- Cold-start effect on first inference call: **~0.6s slower** than warm steady-state — present but
  modest, not the dominant cost. If F-008/Hangfire unloads the model between scheduled runs
  (Architecture §3 Job Scheduler), budget model-load time (~5-6s) plus this modest first-call
  delta separately from steady-state per-repo cost, per §3.8's original guidance.

### 9.2 Multi-run wall-clock statistics (§3.5/§3.7, n=10 per size, real data)

| README size | n | mean | p50 | p95 | max |
|---|---|---|---|---|---|
| small (1,462 B) | 10 | 2.684s | 2.675s | 2.804s | 2.804s |
| mid (2,056 B) | 10 | 2.568s | 2.576s | 2.601s | 2.601s |
| large (3,834 B) | 10 | 2.607s | 2.590s | 2.816s | 2.816s |

Per §3.7's own caveat, p95 at n=10 equals max (nearest-rank needs n≥20 to diverge) — these are
"max observed at 10 samples," not true 95th-percentile estimates, but see §9.6 for why that
caveat doesn't matter here.

**Mean-vs-p95 tail check (§5):** ratio is ~1.02-1.08x across all three sizes — no tail-latency
blowup; the "occasional slow run" risk §5 warns about did not materialize in this run.

**README size had negligible effect on wall-clock time** (2.57-2.68s mean across a 1.5-3.8 KB
range) — consistent with `max_tokens: 300` capping output length as the dominant cost driver, not
input/prompt length, at these input sizes.

### 9.3 Native token-level stats (§3.6, `/api/v0/chat/completions`)

The optional native stats endpoint worked as documented, no version mismatch:

```json
{
  "tokens_per_second": 123.71,
  "time_to_first_token": 0.083,
  "generation_time": 2.417,
  "stop_reason": "maxPredictedTokensReached"
}
```

**123.7 tok/s is well above §4's entire estimation bracket** (which topped out at ~80 tok/s for
the "Consumer GPU" tier). Either the operator's hardware is faster than §4's assumed range, or
`google/gemma-4-e4b`'s actual architecture (per its MatFormer-style design goals, discussed as
§4's lowest-confidence claim) genuinely delivers above-tier throughput — this run can't distinguish
those two explanations, but the practical implication (throughput is not a bottleneck) holds
either way.

### 9.4 New finding, not anticipated by §1-§8: reasoning-token budget truncation

Every single response across all 30 timed runs hit `finish_reason: "length"` — the model produces
a separate internal `reasoning_content` field *before* the visible summary, and that reasoning
consumed **195-258 of the 300-token `max_tokens` budget** (65-86%), leaving only **30-60 words** of
actual visible summary content — well short of the "under 150 words" the system prompt (§3.3)
requested. Verified consistent across small/mid/large fixtures (not a one-off):

| Size | completion_tokens | reasoning_tokens | visible content |
|---|---|---|---|
| small | 300 (capped) | 215 | 450 chars (~70 words) |
| mid | 300 (capped) | 195 | ~380 chars (~60 words) |
| large | 300 (capped) | 258 | 226 chars (~35 words) |

**This is not a throughput problem — wall-clock time was fine (§9.2) — it's an output-completeness
problem.** `google/gemma-4-e4b` appears to be a reasoning-capable model that spends a large,
variable fraction of its output budget on internal deliberation before answering, which
`max_tokens: 300` (chosen in §3.3 as a reasonable cap for a "under 150 words" summary, written
without knowledge of this model's reasoning behavior) does not account for. **Action needed before
F-008 implementation, not before A2 sign-off** (see §9.5 for why these are separable):
1. Increase `max_tokens` substantially (e.g. 700-900) to give reasoning room without starving the
   visible answer, and re-measure whether wall-clock time (§9.2) still holds at the higher token
   count — it should, since §9.2 already shows generation time scales with `max_tokens`, not input
   size, so a token-budget increase will proportionally increase wall-clock time and needs
   re-verification against §5's bands, not an inherited pass.
2. Check whether LM Studio/the model exposes a way to suppress or cap reasoning output separately
   from the final answer (some reasoning-capable models support this via API parameters or
   system-prompt instructions) — would be a cleaner fix than just widening the budget.
3. If neither closes the gap, reconsider whether `google/gemma-4-e4b`'s reasoning behavior is
   compatible with a fast, budget-capped summarization use case at all, versus a non-reasoning
   variant.

### 9.5 What A2 resolution actually covers now — and what it doesn't

Risk A2 (Architecture §8) is stated as "local LLM inference throughput/model choice is unproven
against the seconds-per-repo target in NFR-001." §9.1-§9.3 directly answer that question: **the
per-repo wall-clock cost, at the `max_tokens: 300` setting used in this benchmark, is 2.57-2.82s —
squarely in the §5 "Pass" band (≤~30s p95), with no tail-latency concern.** That specific,
literal question is resolved.

**§9.4's finding is a distinct problem from A2's throughput question** — it's about response
*completeness*, not response *speed* — and increasing `max_tokens` to fix it (§9.4 item 1) will
change the wall-clock number measured here. So: **A2 (throughput vs. NFR-001) is resolved as
measured**, but the measurement was taken at a `max_tokens` setting §9.4 shows is inadequate for
real use, meaning **the *specific numbers* in §9.2 should be re-verified once F-008 lands on a
`max_tokens` value that doesn't truncate reasoning models mid-answer** — almost certainly still a
"Pass" per §9.4 item 1's reasoning, but not yet re-measured at that setting. This spike is not
reopening A2 to "unresolved" over that gap (the order-of-magnitude margin to the 30s Pass
threshold is large enough that a 2-3x increase in `max_tokens` is very unlikely to cross it) — it's
flagging the gap explicitly rather than letting it go unstated.

### 9.6 Updated NFR-001 verdict

| Band | §5 threshold | Measured (§9.2) | Verdict |
|---|---|---|---|
| Pass | p95 ≤ ~30s | 2.60-2.82s p95 across all three sizes | **Pass, by a wide margin (~10x headroom)** |

Given that margin, the n=10-not-n=20 p95-equals-max caveat (§3.7, §9.2) doesn't change the
conclusion — even the true (unmeasured) 95th percentile would need to be roughly 10x worse than
the observed max to threaten the Pass verdict, which nothing in this data suggests.

## 10. Model Comparison & Final Decision (2026-08-01) — ADR-017 Supersedes ADR-013

§9.4's reasoning-token truncation finding raised a question §9.4's own mitigation list treated as
last-resort: is `google/gemma-4-e4b` even the right model for this job, versus fixing the symptom
with a wider `max_tokens`? Rather than guess, four already-downloaded alternatives were tested
live against the identical mid-size request (same README, same system prompt, same
`max_tokens: 300`) that had produced `gemma-4-e4b`'s truncated output.

### 10.1 Single-request comparison across candidates

| Model | Wall time | `finish_reason` | Visible output | Reasoning tokens |
|---|---|---|---|---|
| `google/gemma-4-e4b` (§9 baseline) | 2.57-2.82s (p95) | `length` (truncated) | 30-60 words | 195-258 of 300 |
| `gemma-3-4b-it` | 1.33s | `stop` (complete) | ~120 words | 0 |
| `gemma-4-12b-it-qat` | 2.89s | `stop` (complete) | ~125 words | 0 |
| **`llama-3.2-3b-instruct` (chosen)** | **0.88s** | `stop` (complete) | ~90 words | **0** |
| `qwen2.5-coder-7b-instruct` | 1.60s | `stop` (complete) | ~120 words | 0 |

`deepseek/deepseek-r1-0528-qwen3-8b` (also present in the catalog) was **not** tested — "R1" is a
well-established, explicit reasoning-model designator; testing it would have predictably shown the
same class of problem `gemma-4-e4b` did, so a live test was skipped rather than spending a run on
a near-certain negative result. This is a documented judgment call, not an unexamined gap.

**Notable finding:** `gemma-4-12b-it-qat` shares `gemma-4-e4b`'s `gemma4` architecture family but
produced zero reasoning-token overhead. The truncation problem is specific to the `e4b` fine-tune,
not Gemma 4 as an architecture — worth remembering if a future Gemma variant is considered again.

### 10.2 Full-rigor benchmark for the chosen model (n=10 per size, matching §9.2's methodology)

| README size | n | mean | p50 | p95 | max |
|---|---|---|---|---|---|
| small (1,462 B) | 10 | 0.866s | 0.876s | 0.976s | 0.976s |
| mid (2,056 B) | 10 | 0.777s | 0.777s | 0.829s | 0.829s |
| large (3,834 B) | 10 | 1.050s | 0.992s | 1.544s | 1.544s |

Native stats (`/api/v0`, mid, single call): **241.6 tok/s**, `stop_reason: "eosFound"` (natural
completion — the model chose to stop, it wasn't cut off), 165 of 300 completion tokens used (45%
headroom remaining in the budget, not exhausted).

Mean-vs-p95 tail check: large size shows more run-to-run variance than `gemma-4-e4b` did (p95/mean
≈ 1.47x vs. `gemma-4-e4b`'s ~1.02-1.08x) — still far short of the 3x threshold §5 flags as
concerning, and the absolute p95 (1.544s) remains ~19x under the 30s Pass threshold. Noted for
completeness, not a finding that changes the verdict.

### 10.3 Decision

**Llama 3.2 3B Instruct (`llama-3.2-3b-instruct`) replaces Gemma 4 E4B as the summarization
model.** ADR-017 (new) supersedes ADR-013 with the full decision record, alternatives considered,
and consequences. Rationale, in brief: fastest of the five candidates tested, zero reasoning-token
waste, complete natural-stop output, and no functional requirement ties the platform to the Gemma
family specifically once Gemma's own `e4b` variant is what caused the original problem.

**This is not a reversal of §9's Pass verdict for `gemma-4-e4b`** — that model did pass NFR-001 on
throughput. It's a decision that passing throughput wasn't sufficient once the truncation problem
surfaced, and that a better-fitting model was available without trading away speed.

**PM-005 is closed by this decision**, not by the `max_tokens`-increase mitigation it was
originally written around (`docs/project-management.md`).

## Version History
| Version | Date | Change | Triggered By |
|---|---|---|---|
| v1 | 2026-07-31 | Initial spike output — no live LM Studio access; availability assessment, benchmark methodology, estimation framework, NFR-001 comparison framework, and A2 verdict delivered | F-002 Task Packet |
| v1.1 | 2026-08-01 | §2 addendum: model availability confirmed live (`google/gemma-4-e4b`) | Operator confirmed LM Studio running |
| v2 | 2026-08-01 | §3's benchmark actually executed against real LM Studio + `google/gemma-4-e4b`; added §9 (Measured Results, 6 subsections); §7's verdict table updated from "Not resolved" to "Resolved"; §8's follow-ups updated to reflect completion; new finding (reasoning-token budget truncation, §9.4) flagged for F-008 | Operator: "run that spike and update the results" |
| v3 | 2026-08-01 | Added §10 — live comparison against 4 alternative models, full-rigor benchmark for `llama-3.2-3b-instruct`, and the resulting model swap; ADR-017 (new) supersedes ADR-013; title/status header updated to reflect the final pick is not Gemma 4 E4B | Operator: "use llama-3.2-3b-instruct, update docs. update spike and ADRs" |
