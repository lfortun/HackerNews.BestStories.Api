# HackerNews.BestStories.Api

RESTful API built with ASP.NET Core (.NET 10) that returns the details of the best **n**
stories from [Hacker News](https://github.com/HackerNews/API), ordered by score in
descending order.

**Santander - Developer Coding Test.**

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (`dotnet --version` must report `10.x`).

## How to run

```bash
dotnet run --project HackerNews.BestStories.Api
```

The API will be available at `http://localhost:5241`.

- Swagger UI (Development only): `http://localhost:5241/swagger`
- OpenAPI spec: `http://localhost:5241/openapi/v1.json`

### Usage

```bash
curl "http://localhost:5241/api/stories/best/5"
```

Returns an array with the **5** best stories, for example:

```json
[
  {
    "title": "A uBlock Origin update was rejected from the Chrome Web Store",
    "uri": "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
    "postedBy": "ismaildonmez",
    "time": "2019-10-12T13:43:01+00:00",
    "score": 1716,
    "commentCount": 572
  }
]
```

| Parameter | Description |
|---|---|
| `{n}` | Number of stories to return. Positive integer (route `api/stories/best/{n:int}`). |

- `n <= 0` → `400 Bad Request`.
- Non-numeric `n` → never reaches the controller (`{n:int}` route constraint).
- Unhandled errors → `500 Internal Server Error` (global exception middleware).

## Architecture

Clean Architecture across 3 projects (`HackerNews.BestStories.slnx`):

```
Api -> Infrastructure -> Application
  \---- (Api references Infrastructure and Application explicitly; no cycles)
```

- **Api**: controllers, middleware, DI composition.
- **Infrastructure**: HTTP client (`HackerNewsClient`), cache decorator, Polly.
- **Application**: query (`GetBestStoriesQuery`), DTOs, interfaces, options.

Flow: `StoriesController` → `GetBestStoriesQuery` → `IHackerNewsClient` (cache → HTTP).

### Efficiency and protecting the Hacker News API

- **In-memory cache**: best-story IDs (1 minute time to live) and per-story details
  (15 minutes time to live), with **single-flight**: a shared `Lazy<Task<T>>` is cached so
  concurrent cold-cache misses coalesce into a single upstream call; failed fetches are
  evicted so the next request retries, and one caller disconnecting does not abort the
  shared fetch for the others.
- **Bounded concurrency**: detail requests run in parallel with a maximum of 10
  concurrent requests (`SemaphoreSlim`), avoiding socket saturation and API overload.
- **Retries**: Polly with exponential backoff (2s, 4s, 8s) on transient errors (5xx, 408),
  each attempt bounded by its own timeout (seconds from `HackerNewsApi:Timeout`).

## Design patterns and SOLID principles

### Design patterns

- **Decorator**: `HackerNewsCacheDecorator` wraps the real `HackerNewsClient` (both
  implement `IHackerNewsClient`) to add caching transparently, without modifying the
  underlying implementation.
- **Dependency Injection**: registered composition root in `Infrastructure/DependencyInjection.cs`
  (`AddInfrastructure`) plus constructor injection; the cache decorator is wired via a factory.
- **Options pattern**: the `HackerNewsApi` configuration section maps to the strongly-typed
  `HackerNewsOptions` class (`Configure<T>` + `IOptions<T>`).
- **Separated Interface**: the contracts consumed by higher layers (`IHackerNewsClient`,
  `IGetBestStoriesQuery`) live in the `Application` project, while their concrete
  implementations live in `Infrastructure`/`Application.Services`, keeping dependency
  direction pointing inward.
- **Fast fail / retry policy (Polly)**: transient HTTP errors are retried with exponential
  backoff instead of failing immediately.

### SOLID principles

- **Single Responsibility**: each type has one reason to change — `StoriesController`
  (HTTP concerns), `GetBestStoriesQuery` (orchestration/ordering), `HackerNewsClient`
  (HTTP calls), `HackerNewsCacheDecorator` (caching), `ExceptionHandlingMiddleware`
  (uniform error responses).
- **Open/Closed**: caching is added via the decorator without altering `HackerNewsClient`;
  behaviors are extended through composition rather than modification.
- **Liskov Substitution**: clients depend on `IHackerNewsClient`; the decorator and the
  real client are interchangeable implementations of the same contract.
- **Interface Segregation**: small, focused interfaces (`IHackerNewsClient`,
  `IGetBestStoriesQuery`); nothing depends on members it does not use.
- **Dependency Inversion**: `Application` defines the abstractions (`IHackerNewsClient`,
  `IGetBestStoriesQuery`) and has no knowledge of HTTP/caching details; `Infrastructure`
  depends on and implements those abstractions, so high-level modules are decoupled from
  low-level ones.

## Assumptions

- Stories that resolve to `null` during fetch (missing `title`, `by` or `url`) are
  **filtered out** (graceful degradation) instead of failing the whole request.
- The final ordering is computed by `score` descending over the stories that survived the
  filter; if fewer than `n` valid stories exist, the available ones are returned.
- The cache is in-memory per instance (fine for a single-instance deployment; use
  [IDistributedCache](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed)
  for multi-instance).
- "Best stories" are the ones returned by Hacker News `beststories.json` endpoint.

## Planned improvements (given the time)

- **URI fallback**: for stories without a `url` (Ask/Show HN), fall back to
  `https://news.ycombinator.com/item?id={id}` instead of `null`.
- **Do not expose `ex.Message`** in the 500 error response (avoids leaking internals).
- **Packaging**: Dockerfile so it runs without a local SDK.

## Configuration

Section `HackerNewsApi` in `appsettings.json`:

```json
{
  "HackerNewsApi": {
    "BaseUrl": "https://hacker-news.firebaseio.com/v0/",
    "Timeout": 15
  }
}
```

## Tests

```bash
dotnet test
```

xUnit + Moq, covering the happy path and the main error case of each unit:

- **`GetBestStoriesQueryTests`**: ordering by score descending and full field mapping
  (including unix epoch → UTC `DateTimeOffset`); null stories are filtered without
  throwing (graceful degradation).
- **`HackerNewsCacheDecoratorTests`**: IDs and story details are served from cache
  (inner client invoked once); single-flight coalesces concurrent cold-cache misses and
  one waiter disconnecting does not abort the shared fetch; a `null` result propagates
  without throwing.
- **`HackerNewsClientTests`**: `beststories.json` deserialization and story detail
  mapping; a server error on story details returns `null` instead of throwing;
  cancellation propagates.
- **`StoryResponseTests`**: serialization produces the exact wire contract
  (`title`, `uri`, `postedBy`, `time`, `score`, `commentCount`).
- **`StoriesControllerTests`**: valid `n` returns the stories; `n <= 0` returns
  `400 Bad Request` without executing the query.