---
name: summary-of-a-period
description: What was saved or changed in the memory over a period
when: the person asks for a summary of a week, a month or another period
enabled: true
---
1. Settle the period: the one the person names, or the last 7 days.
2. From the list of what the memory holds, pick what changed in that period.
3. For a collection with a date field, count and total the entries of the period with collection_aggregate (dateColumn, from, to). For a document, read the part that changed.
4. Answer grouped by topic, short, with the exact numbers the tools gave.
