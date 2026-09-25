# CLAUDE.md — Spincio

Digital card game "Spincio" (house-rules variant of Spazzino), 2v2. Blazor WebAssembly PWA + pure C# engine shared with an ASP.NET Core/SignalR server.

## Source of truth
- **Rules:** `docs/SPEC.md` (Regolamento v1.2). Never invent a rule. If a rule is missing or ambiguous, stop and ask the owner.
- **Acceptance tests `AT-xx`** in `docs/SPEC.md` are the contract. Every rule has an `AT-xx` test with the same ID in its name (e.g. `AT_05_equal_value_forbids_sum`).
- **Decisions:** `docs/adr/`. Changing a decision means a new ADR, not an edit of history.

## Architecture rules
1. `Spincio.Engine` has **zero dependencies**: no `DateTime.Now`, no `System.Random`, no UI, no ASP.NET, no I/O.
2. All randomness goes through `Pcg32`, whose state lives inside `MatchState`.
3. Engine is pure and immutable: `Apply(state, command) → Result<Transition(state, events)>`. Developed in TDD.
4. Every `GameEvent` carries an `Audience` (`All` / `Only(seat)`); hidden information is filtered in the engine, never in the UI.
5. Bots only see `PlayerView` + public-event memory. They never receive `MatchState`. The bot harness (`MatchRunner`) therefore lives in `tools/Spincio.Simulator`, not in `Spincio.Bots` (ADR 0007).
6. Project references (verified with NetArchTest): `Bots → Engine`; `Client → Engine, Bots, Contracts`; `Server → Engine, Bots, Contracts`. `Client` and `Server` never reference each other.
7. Persistence = seed + command log (ADR 0003).

## Property-based invariants (FsCheck)
- The 40 cards are conserved (hands + table + deck + captured piles + unassigned = 40).
- Determinism: same seed + same commands = same states and events.
- Two equal-value cards can be on the table together only if both come from the round's initial table (a dropped card never matches a table card).
- `LegalCommands` is non-empty for the seat to play until the match is over.

## Commands
- Build: `dotnet build Spincio.slnx`
- Test: `dotnet test Spincio.slnx`
- AT ↔ test map: `docs/AT-MAP.md` (keep it updated when adding AT tests).
- Bot tournament: `dotnet run --project tools/Spincio.Simulator -c Release -- --x Greedy --y Random --matches 1000 --seed 1`

## Conventions
- Language: code, identifiers, commit messages in **English**; discussion and docs for the owner in **Italian**.
- Card notation in tests: rank `A,2..7,J,N,K` + suit `D,C,S,B` (e.g. `KD`, `7D`); provide a parser helper in the test project.
- Tests: xUnit + **Shouldly** + FsCheck + bUnit. **Do not use FluentAssertions** (v8 is commercial). No GPL or commercial dependencies.
- Avoid Shouldly `ShouldAllBe`/`ShouldContain(predicate)` inside per-step loops: they compile an expression tree per call.
- Engine: no `yield` iterators (the generated code touches `System.Environment`, which the architecture test forbids).
- Target framework: `net10.0`. Nullable enabled, warnings as errors.

## Legal
- Card art: public-domain Piacentine SVGs from Wikimedia Commons only, each listed in `CREDITS.md`. No Dal Negro / Modiano assets or names.
- Do not use the names "Dal Negro", "Modiano", "Scopa Più".
- `docs/SPEC.md` is written from scratch; never copy rule text from the web.
