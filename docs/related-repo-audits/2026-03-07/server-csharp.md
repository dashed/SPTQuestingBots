# `server-csharp` Audit

Date: 2026-03-07
Repo: `/home/alberto/github/server-csharp`
Branch / revision checked: `main` @ `48067539` (`Merge tag '4.0.12'`)

## Findings

### High: overlapping routers all execute, so route shadowing is order-dependent and can silently overwrite responses

Files:

- `/home/alberto/github/server-csharp/Libraries/SPTarkov.Server.Core/Routers/HttpRouter.cs`
- `/home/alberto/github/server-csharp/Libraries/SPTarkov.Server.Core/DI/Router.cs`
- `/home/alberto/github/server-csharp/Libraries/SPTarkov.DI/DependencyInjectionHandler.cs`

Why this matters:

- `HttpRouter.HandleRoute(...)` iterates every router in the injected collection and executes each one whose `CanHandle(...)` returns `true`.
- For static routes, `CanHandle(url, false)` is exact-match based, so multiple routers with the same URL all run.
- For dynamic routes, `CanHandle(url, true)` is even broader and all matching routers run too.
- The final response is just `wrapper.Output`, so later handlers silently overwrite earlier ones.
- DI registration is sorted by `Injectable.TypePriority`, which gives some control, but when multiple mods target the same route this still becomes fragile and order-sensitive.

QuestingBots relevance:

- QuestingBots shadows `/client/game/bot/generate`, so this dispatch behavior directly affects compatibility with any other mod that also overrides that route.
- This also explains why removing dead dynamic routes is valuable: broad matching increases collision surface.

Recommendation:

- Add a framework-level policy for route conflicts:
  either stop after the first matching router, or require explicit override semantics and reject duplicates at startup.
- Add tests that lock in the intended precedence model for duplicate static routes and overlapping dynamic routes.

### Medium: dynamic route matching uses `Contains(...)`, which is too loose for mod prefixes

Files:

- `/home/alberto/github/server-csharp/Libraries/SPTarkov.Server.Core/DI/Router.cs`
- `/home/alberto/github/server-csharp/Libraries/SPTarkov.Server.Core/Routers/HttpRouter.cs`

Why this matters:

- `DynamicRouter.HandleDynamic(...)` chooses the first route where `url.Contains(r.url)`.
- `Router.CanHandle(..., partialMatch: true)` also uses substring matching.
- This is not a path-segment or prefix match, so similarly named routes can match unintentionally.

Examples of risk:

- `/QuestingBots/Foo` and `/QuestingBots/FooBar` are not disambiguated robustly.
- Two mods with partially overlapping prefixes can both execute for one request.

Recommendation:

- Switch dynamic matching to a prefix or normalized path-segment match.
- Add negative tests proving that similarly named routes do not collide.

### Medium: there appears to be no direct automated coverage for router precedence/collision behavior

Files:

- `/home/alberto/github/server-csharp/Libraries/SPTarkov.Server.Core/Routers/HttpRouter.cs`
- `/home/alberto/github/server-csharp/Libraries/SPTarkov.Server.Core/DI/Router.cs`

What I checked:

- Searched `Testing/` and `Libraries/` for tests covering `HttpRouter`, `DynamicRouter`, or router precedence.
- Found no direct tests for duplicate route handling, overlapping dynamic prefixes, or static-vs-dynamic precedence.

Why this matters:

- The current behavior is subtle and mod-facing.
- Without explicit tests, future changes can break mod overrides or preserve accidental behavior indefinitely.

Recommendation:

- Add focused unit tests for:
  duplicate static routes,
  overlapping dynamic routes,
  precedence ordering by `TypePriority`,
  and single-handler vs multi-handler execution expectations.

## Summary

`server-csharp` is the most important upstream contract for QuestingBots' server plugin, and the main risk I found is router dispatch ambiguity. The current model is permissive enough to support overrides, but it does so by letting every matching router execute and by using loose substring matching for dynamic routes. That combination is workable for a controlled core codebase, but it is brittle for a mod ecosystem where multiple plugins may target the same endpoints.
