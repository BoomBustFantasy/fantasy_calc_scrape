alter table "FantasyCalcPlayerValues" drop constraint "FantasyCalcPlayerValues_mode_check";
alter table "FantasyCalcPlayerValues" add constraint "FantasyCalcPlayerValues_mode_check" check ("mode" in ('DYN', 'RDFT'));
