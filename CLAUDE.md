# CLAUDE.md — Spincio

Digital card game "Spincio" (house-rules variant of Spazzino), 2v2. Blazor WebAssembly PWA + pure C# engine shared with an ASP.NET Core/SignalR server.

## Source of truth
- **Rules:** `docs/SPEC.md` (Regolamento v1.4). Never invent a rule. If a rule is missing or ambiguous, stop and ask the owner. When a rule changes, also bump `LocalGameSession.RulesVersion` and update the in-app rules page (`Components/RulesPanel.razor`).
- **Acceptance tests `AT-xx`** in `docs/SPEC.md` are the contract. Every rule has an `AT-xx` test with the same ID in its name (e.g. `AT_05_equal_value_forbids_sum`).
- **Decisions:** `docs/adr/`. Changing a decision means a new ADR, not an edit of history.
- **Project memory:** `docs/STATUS.md` (milestones, metrics, open decisions), `docs/KNOWLEDGE.md` (architecture map, tuned numbers, lessons learned), `docs/RUNBOOK.md` (how to run, test, deploy, verify). **Read them at the start of a session; update them at the end of every milestone** (checklist in RUNBOOK). Add a "lesson learned" whenever a problem costs more than a few minutes.

## Architecture rules
1. `Spincio.Engine` has **zero dependencies**: no `DateTime.Now`, no `System.Random`, no UI, no ASP.NET, no I/O.
2. All randomness goes through `Pcg32`, whose state lives inside `MatchState`.
3. Engine is pure and immutable: `Apply(state, command) → Result<Transition(state, events)>`. Developed in TDD.
4. Every `GameEvent` carries an `Audience` (`All` / `Only(seat)`); hidden information is filtered in the engine, never in the UI.
5. Bots only see `PlayerView` + public-event memory. They never receive the real `MatchState`. The bot harness (`MatchRunner`) therefore lives in `tools/Spincio.Simulator`, not in `Spincio.Bots` (ADR 0007). Only `PimcBot` handles *hypothetical* states, built from its own view via `SpincioEngine.Hypothetical` (ADR 0008).
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
- Bot tournament: `dotnet run --project tools/Spincio.Simulator -c Release -- --x Greedy --y Random --matches 1000 --seed 1` (`--x Pimc --worlds 16`, `--timing Pimc`)
- Online locally: `dotnet run --project src/Spincio.Server` (port 5080) + `dotnet run --project src/Spincio.Client` (port 5058, Development config points to the local server)
- Browser checks: `tools/browser-checks/*.mjs` (see RUNBOOK)

## Workflow (owner's decision, 07/10/2026)
- Every change goes through a PR into `main`. **Merge it yourself, without asking**, once: CI is green on the current head, there is no conflict, and you have reviewed your own diff for risks (rules vs SPEC, saved games, online play, deploy). The merge publishes the app on GitHub Pages.
- If you find a real risk you cannot resolve, do not merge: explain it to the owner and ask.
- After merging, check that the "Deploy to GitHub Pages" run succeeds (a run stuck in "waiting" blocks the next ones: cancel it so the newer run deploys).

## Conventions
- Language: code, identifiers, commit messages in **English**; discussion and docs for the owner in **Italian**.
- Card notation in tests: rank `A,2..7,J,N,K` + suit `D,C,S,B` (e.g. `KD`, `7D`); provide a parser helper in the test project.
- Tests: xUnit + **Shouldly** + FsCheck + bUnit. **Do not use FluentAssertions** (v8 is commercial). No GPL or commercial dependencies.
- Avoid Shouldly `ShouldAllBe`/`ShouldContain(predicate)` inside per-step loops: they compile an expression tree per call.
- Engine: no `yield` iterators (the generated code touches `System.Environment`, which the architecture test forbids).
- Target framework: `net10.0`. Nullable enabled, warnings as errors. CA1716 is off (C#-only solution).
- Libraries serialized by reflection (Engine, Bots, Contracts) are not marked `IsTrimmable`.
- Wire types go through `Contracts/SpincioJson` (polymorphic `GameEvent`, `Seat` as number); commands travel as `CommandCodec` strings.
- In Blazor, bind inputs that enable buttons with `@bind:event="oninput"`.

## Legal
- Card art: images cut from the Piacentine full-deck scan on Wikimedia Commons, by the owner's decision (ADR 0011, supersedes ADR 0009). That is the only exception: any other third-party asset must be public domain or compatibly licensed and listed in `CREDITS.md`. No other publisher assets, and no Dal Negro / Modiano names.
- Do not use the names "Dal Negro", "Modiano", "Scopa Più".
- `docs/SPEC.md` is written from scratch; never copy rule text from the web.
