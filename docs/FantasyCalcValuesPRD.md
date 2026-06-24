## Problem Statement

FantasyCalc values are currently shaped like a narrow, single-value update problem, but the API can return values for many dynasty format combinations.

Trying to model those combinations as wide columns on `Players` would create an explosion of columns, make future changes painful, and couple a player identity table to a volatile set of scoring formats.

## Solution

Move FantasyCalc dynasty values into a normalized child table that stores one row per player per format combination.

Keep `Players` as the canonical player record and use the new table as the source of truth for FantasyCalc output.

The scraper should fetch every supported dynasty combination, match each returned player to an internal player record, and upsert the corresponding normalized rows.

## User Stories

1. As a fantasy football admin, I want FantasyCalc dynasty values stored per format combination, so that I can support multiple league settings without redesigning the player table.
2. As a fantasy football admin, I want the scraper to upsert values instead of creating duplicate records, so that reruns are safe.
3. As a fantasy football admin, I want the scraper to preserve player identity separately from scoring data, so that player metadata stays stable even when FantasyCalc changes.
4. As a fantasy football admin, I want each player to have values for every supported dynasty combination, so that the app can serve multiple league configurations.
5. As a fantasy football admin, I want the stored values to include the FantasyCalc player identifier, so that I can trace rows back to the API source.
6. As a fantasy football admin, I want the normalized model to include format dimensions such as team size, QB count, PPR, and TE premium, so that the value row is fully self-describing.
7. As a fantasy football admin, I want the storage design to avoid 72+ dynamic columns, so that schema changes stay manageable.
8. As a fantasy football admin, I want future redraft support to fit into the same structure, so that new FantasyCalc modes do not require another redesign.
9. As a fantasy football admin, I want to query values by format combination, so that the app can efficiently look up the right score for a league setting.
10. As a fantasy football admin, I want the model to support nullable metadata like timestamps and optional raw payloads, so that debugging remains possible without changing the core row shape.
11. As a fantasy football admin, I want existing player records to remain the canonical player source, so that player pages and existing integrations do not break.
12. As a fantasy football admin, I want the scraper job to report how many rows were updated, so that I can monitor ingest health.
13. As a fantasy football admin, I want the API client to build the right FantasyCalc query string for each supported setting tuple, so that the fetched data matches the stored row key.
14. As a fantasy football admin, I want the ingest flow to distinguish player rows from draft-pick rows where necessary, so that pick values can be handled intentionally.
15. As a fantasy football admin, I want a deterministic unique key for each value row, so that the same player and format combination always resolves to one record.
16. As a fantasy football admin, I want the stored rows to be indexed by the format dimensions, so that reads remain fast when the dataset grows.
17. As a fantasy football admin, I want the migration path to be incremental, so that the current scraper can keep working while the new table is introduced.
18. As a fantasy football admin, I want the normalized table to support future history/versioning, so that we can preserve prior scrape snapshots if needed later.
19. As a fantasy football admin, I want the data model to be explicit about dynasty-only scope for now, so that the implementation stays focused and testable.
20. As a fantasy football admin, I want the scraping pipeline to remain readable and modular, so that future changes to FantasyCalc format coverage are easy to make.

## Implementation Decisions

- Introduce a dedicated normalized FantasyCalc values table as the source of truth.
- Store one row per player per exact dynasty format combination.
- Include format dimensions in the key: mode, team size, QB count, PPR, and TE premium.
- Treat dynasty as the only supported mode for this first pass.
- Keep `Players` as the canonical identity table and avoid adding a new wide set of value columns there.
- Preserve the existing FantasyCalc player identifier relationship so rows remain traceable.
- Use a unique constraint across the player and format dimensions so ingest can upsert safely.
- Update the scrape job to fetch each supported dynasty combination and persist rows through the database layer.
- Keep the API client responsible for constructing FantasyCalc query parameters for a format tuple.
- Keep the database service responsible for mapping fetched values to persistent records.
- Support future redraft expansion by extending the same normalized structure rather than changing the schema shape again.
- Treat any existing single-value FantasyCalc columns in `Players` as legacy compatibility fields, not the source of truth.

## Testing Decisions

- Good tests should verify external behavior: returned rows, persisted values, unique-key behavior, and job outcomes.
- Good tests should avoid asserting private implementation details such as internal loop structure or temporary dictionaries.
- Test the API query-building behavior with representative dynasty combinations to ensure the correct FantasyCalc request is made.
- Test the normalization/upsert behavior of the new values table to ensure duplicate scrapes do not create duplicate rows.
- Test the scraper job orchestration so that fetching multiple dynasty combinations results in the expected database writes.
- Test error handling paths so that a failed FantasyCalc response does not partially corrupt the stored values.
- Test the database lookup and persistence layer around the new table, because that is the highest-risk integration point.
- Prior art in this repo already uses service-level and job-level logic for ingest work, so tests should follow that same granularity.

## Out of Scope

- Redraft values are out of scope for this PRD.
- A wide `Players` table redesign is out of scope.
- Historical snapshot/version storage is out of scope for the first implementation.
- UI changes in the main app are out of scope.
- Reworking all existing player ranking consumers to use the new table is out of scope unless required for the ingest path.
- Any future normalization of non-FantasyCalc player data is out of scope.

## Further Notes

The most important architectural choice here is to keep the schema aligned with the real shape of the FantasyCalc API instead of forcing that shape into `Players`.

This plan leaves room for redraft later, keeps dynasty ingest deterministic, and avoids a one-off schema explosion that would be hard to unwind later.
