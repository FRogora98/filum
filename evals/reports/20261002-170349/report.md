# LongMemEval — 2026-10-02 17:03

The memory core of spec 030, 2026-10-02. LongMemEval oracle (subset seed 1, 4 per ability, 20 questions) on Gemma 4 31B and DeepSeek V4 Flash, and S (about 50 sessions per question; subset seed 1, 1 per ability, 5 questions) on Gemma 4 31B with the naive reference (BM25, k=5). Host: the sample host, system 'core' = the new core with a consolidation pass after each session (--consolidate), passes run by the host's default model (Gemma 4 31B). 1 repetition: differences of one or two questions are noise, and five S questions are an indication, not a measure. The S run stopped at its cap right after the fifth question. Judge gpt-5.4-mini with the authors' prompts. Earlier tries of this run, stopped and fixed (consolidation on the check model; a failed pass ending the question), cost about 0.22 USD and are not counted here. The first run on the old core is evals/reports/20261001-172311.

50 answers · model cost of this run $2.1755, of every answer $2.1755 (judge included) · 0 agent sessions

| System | Model | Questions × reps | Accuracy (± spread) | extraction | multi-session | knowledge-update | temporal | abstention | Errors | Tokens in / out per question | $ per question | $ per correct | Median s |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| core, S | gemma-4-31b | 5 × 1 | 100.0 % (± 0.0 %) | 100 % | 100 % | 100 % | 100 % | 100 % | 0 | 2582002 / 20748 | $0.2800 | $0.2800 | 1233.0 |
| core, oracle | deepseek-v4-flash | 20 × 1 | 95.0 % (± 0.0 %) | 100 % | 100 % | 100 % | 75 % | 100 % | 0 | 246018 / 9311 | $0.0227 | $0.0238 | 152.4 |
| core, oracle | gemma-4-31b | 20 × 1 | 85.0 % (± 0.0 %) | 100 % | 75 % | 50 % | 100 % | 100 % | 0 | 144465 / 1417 | $0.0154 | $0.0181 | 48.8 |
| naive retrieval (bm25, k=5), S | gemma-4-31b | 5 × 1 | 60.0 % (± 0.0 %) | 100 % | 0 % | 100 % | 0 % | 100 % | 0 | 13531 / 86 | $0.0012 | $0.0021 | 4.6 |
