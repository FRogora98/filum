# LongMemEval — 2026-10-01 17:23

LongMemEval oracle, subset seed 1, 4 per ability (20 questions), 1 repetition, 2026-10-01. Host: the sample host per arm (filum; filum-no-check: Reliability:CheckModel empty; filum-no-revisions: memory_history and memory_undo excluded; filum-episodic: Agent:EpisodicMemory on, spec 029). References called directly through OpenRouter. Judge gpt-5.4-mini with the authors' prompts. The first 100 answers were rebuilt from the run's progress log (no tokens, text or seconds kept). GLM 5.3 Flash had many turns time out at 180 s on OpenRouter: its numbers carry that.

360 answers · model cost of this run $3.5434, of every answer $3.5434 (judge included) · 0 agent sessions

| System | Model | Questions × reps | Accuracy (± spread) | extraction | multi-session | knowledge-update | temporal | abstention | Errors | Tokens in / out per question | $ per question | $ per correct | Median s |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| filum-no-revisions | glm-5.3-flash | 20 × 1 | 100.0 % (± 0.0 %) | 100 % | 100 % | 100 % | 100 % | 100 % | 0 | 109865 / 14761 | $0.0242 | $0.0242 | 177.5 |
| naive retrieval (bm25, k=5) | glm-5.3-flash | 20 × 1 | 100.0 % (± 0.0 %) | 100 % | 100 % | 100 % | 100 % | 100 % | 0 | 5778 / 508 | $0.0011 | $0.0011 | 6.5 |
| naive retrieval (bm25, k=5) | deepseek-v4-flash | 20 × 1 | 100.0 % (± 0.0 %) | 100 % | 100 % | 100 % | 100 % | 100 % | 0 | 5774 / 556 | $0.0005 | $0.0005 | 4.7 |
| filum-episodic | deepseek-v4-flash | 20 × 1 | 95.0 % (± 0.0 %) | 100 % | 100 % | 100 % | 75 % | 100 % | 0 | 154694 / 6550 | $0.0142 | $0.0149 | 143.3 |
| filum-no-check | deepseek-v4-flash | 20 × 1 | 95.0 % (± 0.0 %) | 100 % | 75 % | 100 % | 100 % | 100 % | 0 | 133591 / 5836 | $0.0113 | $0.0119 | 133.9 |
| naive retrieval (bm25, k=5) | gemma-4-31b | 20 × 1 | 95.0 % (± 0.0 %) | 75 % | 100 % | 100 % | 100 % | 100 % | 0 | 6007 / 79 | $0.0006 | $0.0006 | 2.1 |
| filum | glm-5.3-flash | 20 × 1 | 85.0 % (± 0.0 %) | 100 % | 75 % | 100 % | 50 % | 100 % | 2 | 0 / 0 | $0.0195 | $0.0230 | 0.0 |
| filum | deepseek-v4-flash | 20 × 1 | 85.0 % (± 0.0 %) | 100 % | 75 % | 100 % | 50 % | 100 % | 1 | 0 / 0 | $0.0136 | $0.0160 | 0.0 |
| filum-episodic | gemma-4-31b | 20 × 1 | 80.0 % (± 0.0 %) | 100 % | 100 % | 25 % | 75 % | 100 % | 0 | 80788 / 848 | $0.0099 | $0.0123 | 39.9 |
| filum-no-revisions | deepseek-v4-flash | 20 × 1 | 80.0 % (± 0.0 %) | 75 % | 50 % | 100 % | 75 % | 100 % | 0 | 153733 / 6986 | $0.0142 | $0.0178 | 141.4 |
| filum-episodic | glm-5.3-flash | 20 × 1 | 75.0 % (± 0.0 %) | 100 % | 50 % | 75 % | 50 % | 100 % | 2 | 99660 / 11853 | $0.0211 | $0.0282 | 201.4 |
| filum-no-check | glm-5.3-flash | 20 × 1 | 70.0 % (± 0.0 %) | 100 % | 75 % | 75 % | 50 % | 50 % | 5 | 84003 / 11923 | $0.0186 | $0.0265 | 268.6 |
| filum | gemma-4-31b | 20 × 1 | 65.0 % (± 0.0 %) | 100 % | 75 % | 50 % | 50 % | 50 % | 0 | 0 / 0 | $0.0100 | $0.0154 | 0.0 |
| filum-no-revisions | gemma-4-31b | 20 × 1 | 60.0 % (± 0.0 %) | 75 % | 75 % | 25 % | 50 % | 75 % | 0 | 74205 / 919 | $0.0090 | $0.0150 | 29.6 |
| filum-no-check | gemma-4-31b | 20 × 1 | 55.0 % (± 0.0 %) | 75 % | 50 % | 25 % | 25 % | 100 % | 0 | 63236 / 745 | $0.0059 | $0.0108 | 29.1 |
| no memory | glm-5.3-flash | 20 × 1 | 25.0 % (± 0.0 %) | 25 % | 0 % | 0 % | 0 % | 100 % | 0 | 0 / 0 | $0.0005 | $0.0019 | 0.0 |
| no memory | deepseek-v4-flash | 20 × 1 | 25.0 % (± 0.0 %) | 25 % | 0 % | 0 % | 0 % | 100 % | 0 | 68 / 2308 | $0.0004 | $0.0015 | 11.8 |
| no memory | gemma-4-31b | 20 × 1 | 20.0 % (± 0.0 %) | 0 % | 0 % | 0 % | 0 % | 100 % | 0 | 0 / 0 | $0.0002 | $0.0011 | 0.0 |

## Errors (first 20)

- filum-episodic · glm-5.3-flash · aae3761f #1: session of 2023/05/24 (Wed) 15:51: 502 {"type":"https://tools.ietf.org/html/rfc9110#section-15.6.3","title":"The assistant could not answer. Try again.","status":502}
- filum-episodic · glm-5.3-flash · 4f54b7c9 #1: session of 2023/05/20 (Sat) 06:15: 502 {"type":"https://tools.ietf.org/html/rfc9110#section-15.6.3","title":"The assistant could not answer. Try again.","status":502}
- filum-no-check · glm-5.3-flash · 0ddfec37_abs #1: session of 2023/05/25 (Thu) 19:03: 502 {"type":"https://tools.ietf.org/html/rfc9110#section-15.6.3","title":"The assistant could not answer. Try again.","status":502}
- filum-no-check · glm-5.3-flash · 69fee5aa #1: session of 2023/05/20 (Sat) 16:05: 502 {"type":"https://tools.ietf.org/html/rfc9110#section-15.6.3","title":"The assistant could not answer. Try again.","status":502}
- filum-no-check · glm-5.3-flash · aae3761f #1: session of 2023/05/24 (Wed) 15:51: 502 {"type":"https://tools.ietf.org/html/rfc9110#section-15.6.3","title":"The assistant could not answer. Try again.","status":502}
- filum-no-check · glm-5.3-flash · 2ebe6c90 #1: session of 2023/01/10 (Tue) 00:37: 502 {"type":"https://tools.ietf.org/html/rfc9110#section-15.6.3","title":"The assistant could not answer. Try again.","status":502}
- filum-no-check · glm-5.3-flash · 6613b389 #1: session of 2023/07/07 (Fri) 20:06: 502 {"type":"https://tools.ietf.org/html/rfc9110#section-15.6.3","title":"The assistant could not answer. Try again.","status":502}
- filum · glm-5.3-flash · aae3761f #1: the turn failed (rebuilt from the log; the detail was not kept)
- filum · glm-5.3-flash · 9a707b82 #1: the turn failed (rebuilt from the log; the detail was not kept)
- filum · deepseek-v4-flash · gpt4_85da3956 #1: the turn failed (rebuilt from the log; the detail was not kept)
