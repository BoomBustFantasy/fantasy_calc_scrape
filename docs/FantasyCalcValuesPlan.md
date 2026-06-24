# FantasyCalc Values Storage Plan

## Goal

Store FantasyCalc outputs in a normalized table instead of adding a wide set of value columns to `Players`.

This keeps `Players` focused on player identity and shared metadata, while making FantasyCalc values easy to extend, query, and upsert.

## Decision

Use a new child table as the source of truth for FantasyCalc values.

Each row represents one player and one exact FantasyCalc setting combination.

For now, the scope is dynasty only.

## Recommended Table

Table name: `FantasyCalcPlayerValues`

Suggested columns:

- `id` - primary key
- `player_id` - foreign key to `Players.id`
- `mode` - `DYN` for now
- `num_teams` - `8`, `10`, `12`, or `14`
- `num_qbs` - `1` or `2`
- `ppr` - `0`, `0.5`, or `1.0`
- `te_premium` - `NOTEP`, `TEP`, or `TEPPLUS` (API accepts `none`, `te+`, `te++` and values are normalized to uppercase for storage)
- `fantasy_calc_player_id` - FantasyCalc player identifier
- `value` - FantasyCalc value for the combination
- `overall_rank` - overall rank for the combination
- `position_rank` - positional rank for the combination
- `updated_at` - last scrape time

Optional future columns if needed:

- `created_at`
- `source_version`
- `raw_payload` for debugging

## Uniqueness Rule

Add a unique constraint across:

- `player_id`
- `mode`
- `num_teams`
- `num_qbs`
- `ppr`
- `te_premium`

That makes reruns safe and lets the scraper upsert cleanly.

## Why This Is Better Than Wide Columns

- No 72-column explosion on `Players`
- Easy to add new FantasyCalc settings later
- Easy to query by format
- Easy to index
- Easy to keep history later if we decide to version rows instead of overwriting them

## Scraper Behavior

The scraper should:

1. Fetch the dynasty API output for every supported combination.
2. Match each returned player to the internal `Players` row.
3. Upsert one row per player per combination into `FantasyCalcPlayerValues`.
4. Update `updated_at` on each successful scrape.

The scraper should not need to create or manage dozens of format-specific columns on `Players`.

## `Players` Table Handling

Keep `Players` as the canonical player table.

Recommended approach:

- Leave existing `fantasy_calc_player_id` in place if other code still depends on it.
- Treat `fantasy_calc_redraft_value` and `fantasy_calc_dynasty_value` as legacy cache columns, not the source of truth.
- If the app needs a fast default value later, derive it from the normalized table or maintain a separate cache intentionally.

## Example Row Key

Example combination:

- `DYN`
- `12TEAM`
- `2QB`
- `FULLPPR`
- `TEPPLUS`

That would store as one row for that player rather than a dedicated column like `DYN_12TEAM_2QB_FULLPPR_TEPPLUS`.

## Future Expansion

When redraft is added later, the same table can handle it by expanding `mode` to include `RE` as well.

If we ever want to keep historical snapshots, we can add an effective date or a version column without changing the basic structure.

## Implementation Notes

The next code changes should be limited to:

- creating the new Supabase table and unique index
- adding the corresponding C# model
- updating the scraper job to write into the new table
- leaving the current `Players` model untouched except for any compatibility cleanup we decide on later
