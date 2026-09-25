# HackerNews.BestStories.Api

A REST API built with ASP.NET Core (.NET 10) that returns the details of the **n** best
stories from [Hacker News](https://github.com/HackerNews/API), ordered by score.

The whole thing is one endpoint: `GET /api/stories/best/{n}`. Small surface, but it hides
the interesting part — the Hacker News API is public, unversioned and not exactly
generous with its resources, so the real job was making an API that reads well, stays
fast under load, and never punches the upstream harder than it needs to.

**Santander - Developer Coding Test.**

## How to run

You only need the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/lfortun/HackerNews.BestStories.Api.git
cd HackerNews.BestStories.Api
dotnet run --project HackerNews.BestStories.Api
```

The API comes up at `http://localhost:5241`.

- Swagger UI (Development only): `http://localhost:5241/swagger`
- OpenAPI spec: `http://localhost:5241/openapi/v1.json`

### Usage

```bash
curl "http://localhost:5241/api/stories/best/5"
```

Returns an array with the best stories, for example:

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
  }
]
```

| Parameter | Description |
|---|---|
| `{n}` | Number of stories to return. Positive integer (route `api/stories/best/{n:int}`). |

Error handling is consistent across the board:

- `n <= 0` → `400 Bad Request` (`application/problem+json`, RFC 7807).
- Non-numeric `n` → never reaches the controller (`{n:int}` route constraint).
- Unhandled errors → `500 Internal Server Error` (`application/problem+json`); the
  exception `detail` is only exposed in Development, so internals never leak in prod.

## How it works

### The request flow

`StoriesController` → `GetBestStoriesQuery` → `IHackerNewsClient`. The client is
registered as a decorator chain, so the controller has no idea caching even exists — it
just asks for stories and gets them.

### Why three projects

Clean Architecture in a straight line: `Api → Infrastructure → Application`. I kept the
contracts (`IHackerNewsClient`, `IGetBestStoriesQuery`) in `Application` with zero
knowledge of HTTP or caching, and put the mechanics in `Infrastructure`. The pay-off is
that the tests mock the interface, not the network, and nobody in the upper layers has to
care how a story gets fetched. No cycles, everything points inward.

### Caching, and keeping the Hacker News API healthy

This is where I spent most of the effort. The ranking lists barely change, and the story
details are immutable in practice, so re-fetching them per request would be wasted load
on a free public API.

I cache **both** the ID list and the individual details in memory. The interesting part
is the stampede problem: ten simultaneous cold requests hitting the same cache key would
each fire their own upstream call. I solved it with single-flight — a shared
`Lazy<Task<T>>` lives in the cache, so all concurrent misses coalesce into one upstream
call and every caller awaits the same result (`WaitAsync`, so one client disconnecting
doesn't abort the fetch for everyone else). If the upstream fails, the entry is evicted
and the next request tries again.

For the TTLs I started from the Hacker News API's own behavior and tuned from there:

- **Best-story IDs: 60 seconds.** Rankings move; a minute is short enough to stay fresh
  and long enough to soften the load.
- **Story details: 15 minutes.** Details are effectively immutable, so the longer window
  costs nothing in freshness and saves a lot of churn.

Both are configurable via `HackerNewsApi:BestStoryIdsCacheSeconds` and
`HackerNewsApi:StoryDetailsCacheSeconds` (see [Configuration](#configuration)).

### Bounded concurrency

Fetching `n` stories means `n` upstream calls, so I capped the fan-out with a
`SemaphoreSlim` of 10 in-flight detail requests. It keeps sockets from saturating under
load and stops a big `n` from looking like an attack.

### Resilience against transient failures

The Hacker News API occasionally hiccups. Rather than failing fast on the first 5xx, I
configured Polly to retry transient errors (5xx, 408) with exponential backoff (2s, 4s,
8s). One detail here: each retry attempt gets its **own** per-attempt timeout (seconds
from `HackerNewsApi:Timeout`), instead of a fixed `HttpClient.Timeout` that swallows the
whole operation. That way a hang is detected quickly on every individual attempt instead
of once at the end of the whole pipeline.

### Degrading gracefully

Public APIs return dead posts, deleted posts, and the occasional `null`. My rule: a
story that fails to fetch, or comes back without `title`/`by` (which is how dead/deleted
posts typically look), gets **filtered out rather than killing the response**. The query
logs how many were requested vs. discarded, so degradation is visible, not silent. And
since Ask/Show HN stories have no `url`, I fall back to their item page so `uri` is
always populated. The caller gets a 200 with what's valid — I'd rather provide a partial
answer than a brittle one.

### Error handling

Both error paths respond as `ProblemDetails` (RFC 7807, `application/problem+json`): the
controller's `Problem(...)` for validation, and a middleware that catches everything else.
The middleware adds a `traceId` and only reveals exception details in Development — prod
logs stay clean of internals.

## Assumptions

- **TTL trade-off**: short TTL for rankings (1 min) and a longer one for stories (15 min)
  is deliberate — it keeps answers fresh while avoiding overloading the Hacker News API,
  which is a free shared resource.
- Partial answers are acceptable: if fewer than `n` valid stories exist (fetches that
  fail, dead/deleted posts), the service returns the ones that survive the filter with a
  200 instead of failing the call.
- The cache is in-memory **per instance**, which is fine for a single-instance
  deployment. For multiple containers it would need a distributed cache (see below).
- "Best stories" means whatever `beststories.json` returns, ordered by score descending.

## Future enhancements (if this were going to production)

- **Distributed cache** (e.g. Redis via `IDistributedCache`) so multiple containers
  share one cache instead of each serving its own copy.
- **Observability**: metrics and traces with OpenTelemetry — request duration, cache hit
  rate, upstream latency, retry counts.
- **Input validation with FluentValidation** (or data annotations) once the surface has
  more than one endpoint.
- **Packaging**: a Dockerfile and a CI pipeline, so it runs anywhere without a local SDK.

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

15 tests (xUnit + Moq). The pattern I follow is one happy path and one failure case per
unit, so every behavior has a counter-example:

- **`GetBestStoriesQueryTests`**: ordering by score and full field mapping (unix epoch →
  UTC); stories that are `null` or missing `title`/`by` are filtered without throwing;
  a missing `url` falls back to the item page.
- **`HackerNewsCacheDecoratorTests`**: cache hit serves without re-invoking the inner
  client; single-flight coalesces concurrent cold misses and one waiter disconnecting
  doesn't abort the shared fetch; a `null` result propagates without throwing.
- **`HackerNewsClientTests`**: contract deserialization and mapping; a server error on
  story details returns `null` instead of throwing; cancellation propagates.
- **`StoryResponseTests`**: serialization produces the exact wire contract.
- **`StoriesControllerTests`**: a valid `n` returns the stories; `n <= 0` returns `400`
  with a `ProblemDetails` body without executing the query.