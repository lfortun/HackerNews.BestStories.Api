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
    "title": "F-Droid 2.0",
    "uri": "https://f-droid.org/2026/09/24/f-droid-2.0-a-new-chapter-for-android-freedom.html",
    "postedBy": "daveoc64",
    "time": "2026-09-24T15:26:12+00:00",
    "score": 896,
    "commentCount": 256
  },
  {
    "title": "Italian parliament votes for return to nuclear energy",
    "uri": "https://apnews.com/article/italy-nuclear-chernobyl-4891b6b7c7791ae84db6b0bf0f7cf567",
    "postedBy": "geox",
    "time": "2026-09-23T17:06:16+00:00",
    "score": 873,
    "commentCount": 758
  },
  {
    "title": "Claude discovers a novel enzyme system with CRISPR-like repeats",
    "uri": "https://www.anthropic.com/news/claude-discovers-novel-enzyme-system",
    "postedBy": "raahelb",
    "time": "2026-09-23T18:06:47+00:00",
    "score": 757,
    "commentCount": 781
  },
  {
    "title": "Jev in 25 Lines of Python",
    "uri": "https://www.nobodywho.ai/posts/jev-in-25-lines/",
    "postedBy": "bashbjorn",
    "time": "2026-09-23T07:26:23+00:00",
    "score": 672,
    "commentCount": 209
  },
  {
    "title": "Meta takes down a critical video about meta AI Glasses after filming at Meta",
    "uri": "https://www.reddit.com/r/facebook/comments/1wotwrk/meta_takes_down_a_critical_video_about_meta_ai/",
    "postedBy": "pieterr",
    "time": "2026-09-24T08:23:03+00:00",
    "score": 596,
    "commentCount": 357
  }
]
```

| Parameter | Description |
|---|---|
| `{n}` | Number of stories to return. Positive integer (route `api/stories/best/{n:int}`). |

- `n <= 0` → `400 Bad Request` (`application/problem+json`, RFC 7807).
- Non-numeric `n` → never reaches the controller (`{n:int}` route constraint).
- Unhandled errors → `500 Internal Server Error` (`application/problem+json`); the exception
  `detail` is only included in Development (avoids leaking internals in production).

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

- **In-memory cache**: best-story IDs and per-story details in memory (time to live
  configurable via `HackerNewsApi:BestStoryIdsCacheSeconds` = 60s and
  `HackerNewsApi:StoryDetailsCacheSeconds` = 900s by default), with **single-flight**: a
  shared `Lazy<Task<T>>` is cached so concurrent cold-cache misses coalesce into a single
  upstream call; failed fetches are evicted so the next request retries, and one caller
  disconnecting does not abort the shared fetch for the others.
- **Bounded concurrency**: detail requests run in parallel with a maximum of 10
  concurrent requests (`SemaphoreSlim`), avoiding socket saturation and API overload.
- **Retries**: Polly with exponential backoff (2s, 4s, 8s) on transient errors (5xx, 408),
  each attempt bounded by its own timeout (seconds from `HackerNewsApi:Timeout`).
- **Observability**: `GetBestStoriesQuery` logs a warning with the requested/discarded
  counts whenever the graceful-degradation filter drops stories.

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

- Stories that resolve to `null` during fetch, or that are missing a `title`/`by` (which is
  how dead/deleted posts typically come back), are **filtered out** (graceful degradation)
  instead of failing the whole request; a story without a `url` (Ask/Show HN) falls back to
  `https://news.ycombinator.com/item?id={id}` so `uri` is always populated.
- The final ordering is computed by `score` descending over the stories that survived the
  filter; if fewer than `n` valid stories exist, the available ones are returned.
- The cache is in-memory per instance (fine for a single-instance deployment; use
  [IDistributedCache](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed)
  for multi-instance).
- "Best stories" are the ones returned by Hacker News `beststories.json` endpoint.

## Planned improvements (given the time)

- **Packaging**: Dockerfile so it runs without a local SDK.

## Configuration

Section `HackerNewsApi` in `appsettings.json`:

```json
{
  "HackerNewsApi": {
    "BaseUrl": "https://hacker-news.firebaseio.com/v0/",
    "Timeout": 15,
    "BestStoryIdsCacheSeconds": 60,
    "StoryDetailsCacheSeconds": 900
  }
}
```

## Tests

```bash
dotnet test
```

xUnit + Moq, covering the happy path and the main error case of each unit:

- **`GetBestStoriesQueryTests`**: ordering by score descending and full field mapping
  (including unix epoch → UTC `DateTimeOffset`); null/missing-`title`/missing-`by` stories
  are filtered without throwing, and a missing `url` falls back to the item page.
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
  `400` with a `ProblemDetails` body without executing the query.