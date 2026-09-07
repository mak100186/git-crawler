# LinkedIn post — the hidden-gem scoring algorithm

Draft for the post accompanying `docs/diagrams/output.gif`. Tone brief: a learner sharing an
experiment and its open problems, not a launch announcement.

---

I've been building a side project that hunts for under-the-radar GitHub repositories. I assumed the
hard part would be the crawling. It wasn't. The hard part was admitting what a score actually is.

Because the moment you rank repositories, you're passing judgment on someone's hard work. And I'm in
no position to tell one person their craft is a 42 and another's is an 81.

So I changed the question the score answers.

**It's not "how good is this repository." It's "how likely is it that you've never seen this?"**

That reframing fixed the design, and it broke my star counter.

Every discovery tool I looked at treats stars as a ladder — more is better, all the way up. Follow
that to its conclusion and the ideal repository is the single most-starred project on GitHub. That
is not a result you need a crawler to find.

So stars now score on a bell curve across 12 buckets. Bucket 1 (0–100 stars) and bucket 12 (100,000+)
score identically — about 0.09 each. Both tails are unhelpful, for opposite reasons: the left is
unproven, the right is already famous.

Which means Linux scores 0.09 on the star signal. That number isn't a criticism of Linux. It means
"you already know about Linux," and nothing else.

For anyone who wants the actual maths, it's pleasingly small.

Stars are bucketed first, geometrically, because star counts are distributed that way — 0–100,
101–250, 251–500, and so on up to 100,001+. Twelve buckets. The curve then scores the **bucket
index**, not the raw star count:

```
score(i) = exp( -(i - 6.5)² / 2σ² ) / peak     where σ = 2.5, i = 1..12
```

Two details in there took me longer to get right than they should have.

**The centre is 6.5, not 6 or 7.** That's the midpoint of 1..12, so buckets 6 and 7 both sit exactly
0.5 away and share the peak. It makes the curve properly symmetric — 1 pairs with 12, 2 with 11, 3
with 10 — which is the actual requirement. σ isn't doing that work; σ only sets how steeply the
score falls toward the tails.

**The `/ peak` divisor is normalisation, not decoration.** A raw Gaussian never quite reaches 1.0 at
i = 6.5 ± 0.5, so I divide by `exp(-0.25 / 2σ²)` to pin the maximum at exactly 1.0. Without it the
star signal would quietly max out below its own 50% weight, and it would be the only one of the five
that couldn't reach the top of its range. Every component lives in [0,1] or the weights stop meaning
what they say.

Plug in i = 1: `exp(-30.25 / 12.5) / 0.98 ≈ 0.09`. That's the number Linux gets.

Worth naming the tradeoff: bucketing before curving means a 251-star repo and a 500-star repo score
identically. That's deliberate — I'd rather be honestly coarse than invent precision the underlying
signal doesn't have — but it is a real edge, and repos sitting near a boundary are treated more
arbitrarily than I'd like.

The caveats I keep chewing on, and haven't solved:

→ σ = 2.5 on that curve is a judgment call, not a discovery. It sets how fast the score falls off
toward the tails, and I picked it because the output looked sane. That's not the same as right.

→ The left tail conflates "nobody has looked at this" with "nobody has _found_ this." Those are very
different, and star count alone genuinely can't tell them apart. This is the weakness I'd most like
to fix.

→ Stars measure attention, not quality. A bell curve over a proxy is still a curve over a proxy.

→ 50/20/15/10/5 across the five signals is my opinion, expressed as arithmetic.

The one thing I'm confident about is a UI decision, not a maths one: never show the number alone.
Open any repository and the five signals that produced it are broken out individually, so you can
disagree with my weighting and read the raw evidence yourself. A single number that can't be argued
with is just an assertion.

Genuinely curious how others have approached this — particularly anyone who's tried to separate
"undiscovered" from "not yet built." I don't think star count gets you there.

#OpenSource #SoftwareEngineering #dotnet #Angular
