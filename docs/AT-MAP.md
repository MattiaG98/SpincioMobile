# Mappa AT → test

Ogni test di accettazione della [SPEC](SPEC.md) e il test che lo verifica (`tests/Spincio.Engine.Tests/Acceptance/`).
Per eseguirli: `dotnet test Spincio.slnx --filter "FullyQualifiedName~AT_"`.

| AT | Test | File |
|---|---|---|
| AT-01 | `AT_01_new_round_deals_three_cards_each_four_on_table_24_in_deck` | `SetupTests.cs` |
| AT-02 | `AT_02_reshuffle_keeps_the_same_dealer` | `SetupTests.cs` |
| AT-02 | `AT_02_two_or_more_aces_on_table_reshuffles_deterministically` | `SetupTests.cs` |
| AT-03 | `AT_03_seat_after_dealer_plays_first_and_dealer_rotates` | `SetupTests.cs` |
| AT-04 | `AT_04_same_seed_and_commands_give_identical_states_and_events` | `SetupTests.cs` |
| AT-05 | `AT_05_equal_value_forbids_sum` | `CaptureTests.cs` |
| AT-06 | `AT_06_sum_capture` | `CaptureTests.cs` |
| AT-07 | `AT_07_player_chooses_among_sums` | `CaptureTests.cs` |
| AT-08 | `AT_08_king_captures_by_sum` | `CaptureTests.cs` |
| AT-09 | `AT_09_king_captures_face_card_plus_number` | `CaptureTests.cs` |
| AT-10 | `AT_10_two_equal_cards_take_one_of_them` | `CaptureTests.cs` |
| AT-11 | `AT_11_must_capture_only_with_the_card_played` | `CaptureTests.cs` |
| AT-12 | `AT_12_card_that_cannot_capture_stays_on_table` | `CaptureTests.cs` |
| AT-13 | `AT_13_sweep_scores_one_point_immediately` | `SweepTests.cs` |
| AT-14 | `AT_14_sweep_on_last_play_does_not_count` | `SweepTests.cs` |
| AT-15 | `AT_15_leftover_table_goes_to_last_capturing_team_without_sweep` | `SweepTests.cs` |
| AT-16 | `AT_16_hand_points` | `DeclarationTests.cs` |
| AT-17 | `AT_17_zero_point_hand_cannot_declare` | `DeclarationTests.cs` |
| AT-18 | `AT_18_declare_adds_team_points_once` | `DeclarationTests.cs` |
| AT-18 | `AT_18_declare_only_on_own_turn_before_first_play_of_the_deal` | `DeclarationTests.cs` |
| AT-19 | `AT_19_others_see_only_type_and_points` | `DeclarationTests.cs` |
| AT-20 | `AT_20_declaration_on_second_deal` | `DeclarationTests.cs` |
| AT-20 | `AT_20_new_deal_reopens_declarations` | `DeclarationTests.cs` |
| AT-21 | `AT_21_more_cards_scores_one_tie_scores_zero` | `RoundScoringTests.cs` |
| AT-22 | `AT_22_more_coins_scores_one_tie_scores_zero` | `RoundScoringTests.cs` |
| AT-23 | `AT_23_rebello_and_settebello` | `RoundScoringTests.cs` |
| AT-24 | `AT_24_primiera_is_most_sevens` | `RoundScoringTests.cs` |
| AT-25 | `AT_25_napola` | `RoundScoringTests.cs` |
| AT-26 | `AT_26_all_ten_coins_wins_even_when_behind` | `RoundScoringTests.cs` |
| AT-27 | `AT_27_reaching_31_mid_round_does_not_end_the_match` | `MatchEndTests.cs` |
| AT-28 | `AT_28_both_over_31_higher_wins` | `MatchEndTests.cs` |
| AT-29 | `AT_29_first_to_31_wins` | `MatchEndTests.cs` |
| AT-30 | `AT_30_tie_at_31_or_more_starts_a_tiebreak` | `MatchEndTests.cs` |
| AT-30 | `AT_30_tiebreak_restarts_from_zero_and_repeats_while_tied` | `MatchEndTests.cs` |
| AT-31 | `AT_31_view_has_own_hand_table_score_declarations_and_hand_counts` | `HiddenInformationTests.cs` |
| AT-31 | `AT_31_view_never_exposes_captured_piles` | `HiddenInformationTests.cs` |
| AT-32 | `AT_32_nobody_captured_leftover_goes_to_nobody` | `HiddenInformationTests.cs` |

Test aggiuntivi sui casi limite: `C1_…`, `C6_…`, `F7_all_coins_also_wins_a_tiebreak_match`. Invarianti property-based in `PropertyTests.cs`.
