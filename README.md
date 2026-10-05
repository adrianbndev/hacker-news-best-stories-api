[Español](#español) | [English](#english)

## Español

### Hacker News — API de mejores historias

API REST en ASP.NET Core (.NET 10) que devuelve las `n` mejores stories de Hacker
News ordenadas por `score` descendente sin cargar la API de HN en el camino del
request.

La aplicación sigue una arquitectura hexagonal (puertos y adaptadores). El caso de
uso lee un snapshot en memoria; un `BackgroundService` resuelve los items de HN y
reemplaza el snapshot de forma atómica. Al arrancar se hace un warm-up del snapshot
antes de servir. Los requests nunca llaman a HN.

```
src/
├─ HackerNews.Domain          Story y política de orden/recorte. Sin dependencias.
├─ HackerNews.Application     Puertos (inbound/outbound) y caso de uso.
├─ HackerNews.Infrastructure  Cliente HTTP de HN con Polly, store en memoria, refresco.
└─ HackerNews.Api             Controlador, composición (DI) y OpenAPI.
tests/
├─ HackerNews.Application.UnitTests
├─ HackerNews.Infrastructure.UnitTests
└─ HackerNews.Api.IntegrationTests
```

#### Requisitos

- .NET SDK 10
- Docker (opcional)

#### Cómo ejecutar

Con dotnet:

```
dotnet run --project src/HackerNews.Api
```

Con el perfil `http`, la API queda en `http://localhost:5199`. En Development,
Swagger está disponible en `http://localhost:5199/swagger`.

```
curl "http://localhost:5199/api/stories/best?n=10"
```

Con Docker:

```
docker build -t hacker-news-api .
docker run --rm -p 8080:8080 hacker-news-api
```

```
curl "http://localhost:8080/api/stories/best?n=10"
```

La imagen es multi-stage y arranca como usuario no root.

Tests y formato:

```
dotnet test
dotnet format --verify-no-changes
```

Los tests son herméticos: la API de HN siempre se sustituye por un
`HttpMessageHandler` de prueba o por un doble de `IHackerNewsClient`, nunca se
accede a la red.

#### Endpoint

`GET /api/stories/best?n={n}`

- `n` es opcional; por defecto `10`. Cualquier `n >= 1` es válido: no tiene cota
  superior.
- `200` devuelve un array JSON ordenado por `score` descendente.
- `400` devuelve `ProblemDetails` (title `Invalid story count`) si `n` no es
  numérico, desborda `int`, o es menor que `1`.
- `503` devuelve `ProblemDetails` (title `Best stories are not ready yet`) con la
  cabecera `Retry-After: 5` solo si, tras el warm-up de arranque, el snapshot sigue
  vacío (HN caído).
- Si `n` supera lo disponible, devuelve todo lo disponible.
- «Disponible» es lo que hay en el snapshot: hasta `MaxStories` mejores de HN
  (opcional; ausente = todos) tras descartar items inválidos, así que puede ser
  menos que lo que expone HN.

Ejemplo de respuesta:

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

#### Configuración

Sección `HackerNews` en `appsettings.json`. Cualquier clave se puede sobrescribir
por variable de entorno, por ejemplo `HackerNews__RefreshIntervalSeconds=30`.

| Clave | Valor por defecto | Descripción |
| --- | --- | --- |
| `BaseUrl` | `https://hacker-news.firebaseio.com/v0/` | Base de la API de Hacker News. |
| `RefreshIntervalSeconds` | `60` | Intervalo entre refrescos del snapshot (rango `1..86400` s). |
| `MaxConcurrency` | `16` | Peticiones de item simultáneas durante un refresco. |
| `MaxStories` | `(ausente)` | Opcional: máximo de ids que se resuelven en cada refresco. Ausente = todos los best de HN; si se define, debe ser `>= 1`. |
| `TimeoutSeconds` | `10` | Timeout por intento HTTP, cuerpo incluido (rango `1..86399` s). |
| `RetryCount` | `3` | Reintentos con backoff exponencial y jitter (rango `0..10`). |
| `RetryBaseDelayMilliseconds` | `200` | Base del backoff (rango `0..86400000` ms). |
| `CircuitBreakerFailures` | `5` | Fallos consecutivos antes de abrir el circuito (mínimo `2`). |
| `CircuitBreakerSeconds` | `30` | Ventana de muestreo y duración de apertura (rango `1..86400` s). |

La configuración se valida al arrancar y falla rápido con `OptionsValidationException`
antes de construir el pipeline de Polly:

- `BaseUrl` debe ser una URL absoluta `http`/`https`, sin query ni fragment.
- `RefreshIntervalSeconds` debe estar entre `1` y `86400`.
- `MaxConcurrency` debe ser mayor que cero.
- `MaxStories` es opcional; si se define, debe ser mayor o igual que `1` (sin cota
  superior).
- `TimeoutSeconds` debe estar entre `1` y `86399` (Polly exige un timeout menor de 24 h).
- `RetryCount` debe estar entre `0` y `10`.
- `RetryBaseDelayMilliseconds` debe estar entre `0` y `86400000` (máximo de 24 h de
  `RetryStrategyOptions.Delay`).
- `CircuitBreakerFailures` debe ser al menos `2` (mínimo de Polly).
- `CircuitBreakerSeconds` debe estar entre `1` y `86400` (máximo de 24 h de
  `SamplingDuration`/`BreakDuration`).

Todas las claves que alimentan Polly están acotadas, de modo que ningún valor válido
para el validador hace fallar `ResiliencePipeline.Build()`. El named client de
`HttpClient` usa `Timeout.InfiniteTimeSpan`: el único timeout efectivo es el de Polly.

#### Supuestos

- `beststories.json` devuelve hasta ~500 ids (hoy ~200) ordenados por ranking, no por
  score. El orden por score se calcula una sola vez en background, fuera del request.
- `n` no tiene cota superior: cualquier `n >= 1` se acepta y se devuelve lo
  disponible (`BestStoriesLimits.MinimumCount` es `1`). `MaxStories` es opcional:
  ausente = se materializan todos los ids de `beststories.json`; si se define, acota
  cuántos se resuelven y por tanto puede reducir lo disponible.
- Al arrancar se hace un warm-up del snapshot (con timeout de 15 s) antes de servir:
  si HN responde, el primer request ya devuelve datos y no hay `503`. Después del
  warm-up, el snapshot se refresca cada `RefreshIntervalSeconds` (primer tick tras
  el intervalo). Si un refresco falla, se conserva el snapshot anterior
  (stale-while-error) y se registra el error. El endpoint solo devuelve `503` con
  `Retry-After` si, tras el warm-up, el snapshot sigue vacío (HN caído).
- Un item que falla al resolverse se omite y el refresco continúa con el resto. Si
  no se obtiene ninguna historia, se conserva el snapshot anterior en vez de
  vaciarlo.
- `uri` usa el campo `url` del item; si es nulo o vacío, se usa
  `https://news.ycombinator.com/item?id={id}`.
- `postedBy` usa `by`; si es nulo, se devuelve cadena vacía.
- `time` son segundos Unix. Si quedan fuera del rango representable por
  `DateTimeOffset`, el item se descarta.
- `commentCount` usa `descendants`; si falta, vale `0`.
- Se descartan items nulos, con `type` distinto de `story`, o sin `title`/`score`.
- Un timeout de un intento (`TimeoutRejectedException`) no dispara reintentos ni
  cuenta para el circuit breaker. Es una decisión de diseño: el timeout es la
  estrategia más interna y no está entre los `ShouldHandle` de retry ni del
  circuit breaker. Incluirlo haría que un HN lento abriese el circuito.
- `BaseUrl` puede llevar barra final o no; las rutas relativas del adaptador se
  resuelven siempre bajo la base configurada (`https://host/v0` y
  `https://host/v0/` son equivalentes).
- El snapshot vive en memoria del proceso. Con varias réplicas, cada una mantiene
  el suyo.
- El catálogo de HN es público: el endpoint no requiere autenticación.

#### Mejoras

- Compartir el snapshot entre réplicas (por ejemplo, cache distribuida) para
  arranque en caliente y consistencia.
- Refresco incremental en lugar de recargar los `MaxStories` items.
- Cache HTTP con `ETag`/`Cache-Control` y compresión de respuesta.
- Métricas y health checks: antigüedad del snapshot, duración del refresco,
  estado del circuit breaker.
- Manejar `deleted`/`dead` y filtrar historias retiradas.
- Paginación y consulta por id.

## English

### Hacker News — Best Stories API

REST API built with ASP.NET Core (.NET 10) that returns the top `n` Hacker News
stories ordered by `score` descending without hitting the HN API on the request
path.

The application follows a hexagonal architecture (ports and adapters). The use case
reads an in-memory snapshot; a `BackgroundService` resolves the HN items and
replaces the snapshot atomically. At startup the snapshot is warmed up before
serving. Requests never call HN.

```
src/
├─ HackerNews.Domain          Story plus ordering/trimming policy. No dependencies.
├─ HackerNews.Application     Ports (inbound/outbound) and use case.
├─ HackerNews.Infrastructure  HN HTTP client with Polly, in-memory store, refresh.
└─ HackerNews.Api             Controller, composition (DI) and OpenAPI.
tests/
├─ HackerNews.Application.UnitTests
├─ HackerNews.Infrastructure.UnitTests
└─ HackerNews.Api.IntegrationTests
```

#### Requirements

- .NET SDK 10
- Docker (optional)

#### How to run

With dotnet:

```
dotnet run --project src/HackerNews.Api
```

With the `http` profile, the API listens on `http://localhost:5199`. In Development,
Swagger is available at `http://localhost:5199/swagger`.

```
curl "http://localhost:5199/api/stories/best?n=10"
```

With Docker:

```
docker build -t hacker-news-api .
docker run --rm -p 8080:8080 hacker-news-api
```

```
curl "http://localhost:8080/api/stories/best?n=10"
```

The image is multi-stage and runs as a non-root user.

Tests and formatting:

```
dotnet test
dotnet format --verify-no-changes
```

The tests are hermetic: the HN API is always replaced by a test
`HttpMessageHandler` or a fake `IHackerNewsClient`; the network is never touched.

#### Endpoint

`GET /api/stories/best?n={n}`

- `n` is optional; it defaults to `10`. Any `n >= 1` is valid: there is no upper
  bound.
- `200` returns a JSON array ordered by `score` descending.
- `400` returns `ProblemDetails` (title `Invalid story count`) when `n` is not
  numeric, overflows `int`, or is less than `1`.
- `503` returns `ProblemDetails` (title `Best stories are not ready yet`) with the
  `Retry-After: 5` header only when, after the startup warm-up, the snapshot is
  still empty (HN down).
- If `n` exceeds what is available, it returns everything available.
- "Available" is what the snapshot holds: up to `MaxStories` best HN stories
  (optional; absent = all) after discarding invalid items, so it can be less than
  what HN exposes.

Example response:

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

#### Configuration

`HackerNews` section in `appsettings.json`. Any key can be overridden by environment
variables, for example `HackerNews__RefreshIntervalSeconds=30`.

| Key | Default | Description |
| --- | --- | --- |
| `BaseUrl` | `https://hacker-news.firebaseio.com/v0/` | Hacker News API base. |
| `RefreshIntervalSeconds` | `60` | Interval between snapshot refreshes (range `1..86400` s). |
| `MaxConcurrency` | `16` | Concurrent item requests during a refresh. |
| `MaxStories` | `(absent)` | Optional: maximum ids resolved per refresh. Absent = all best HN stories; when set, must be `>= 1`. |
| `TimeoutSeconds` | `10` | Timeout per HTTP attempt, body included (range `1..86399` s). |
| `RetryCount` | `3` | Retries with exponential backoff and jitter (range `0..10`). |
| `RetryBaseDelayMilliseconds` | `200` | Backoff base (range `0..86400000` ms). |
| `CircuitBreakerFailures` | `5` | Consecutive failures before opening the circuit (minimum `2`). |
| `CircuitBreakerSeconds` | `30` | Sampling window and break duration (range `1..86400` s). |

Configuration is validated at startup and fails fast with `OptionsValidationException`
before building the Polly pipeline:

- `BaseUrl` must be an absolute `http`/`https` URL without query or fragment.
- `RefreshIntervalSeconds` must be between `1` and `86400`.
- `MaxConcurrency` must be greater than zero.
- `MaxStories` is optional; when set, it must be greater than or equal to `1` (no
  upper bound).
- `TimeoutSeconds` must be between `1` and `86399` (Polly requires a timeout under 24 h).
- `RetryCount` must be between `0` and `10`.
- `RetryBaseDelayMilliseconds` must be between `0` and `86400000` (24 h maximum for
  `RetryStrategyOptions.Delay`).
- `CircuitBreakerFailures` must be at least `2` (Polly minimum).
- `CircuitBreakerSeconds` must be between `1` and `86400` (24 h maximum for
  `SamplingDuration`/`BreakDuration`).

Every key that feeds Polly is bounded, so no value accepted by the validator makes
`ResiliencePipeline.Build()` fail. The `HttpClient` named client uses
`Timeout.InfiniteTimeSpan`: the only effective timeout is Polly's.

#### Assumptions

- `beststories.json` returns up to ~500 ids (today ~200) ordered by ranking, not by
  score. Score ordering is computed once in the background, off the request path.
- `n` has no upper bound: any `n >= 1` is accepted and returns what is available
  (`BestStoriesLimits.MinimumCount` is `1`). `MaxStories` is optional: absent means
  every id in `beststories.json` is resolved; when set, it caps how many are
  resolved and can therefore reduce what is available.
- At startup the snapshot is warmed up (15 s timeout) before serving: if HN responds,
  the first request already returns data and there is no `503`. After the warm-up,
  the snapshot refreshes every `RefreshIntervalSeconds` (first tick after the
  interval). If a refresh fails, the previous snapshot is kept (stale-while-error)
  and the error is logged. The endpoint returns `503` with `Retry-After` only when,
  after the warm-up, the snapshot is still empty (HN down).
- An item that fails to resolve is skipped and the refresh continues with the rest.
  If no story is obtained, the previous snapshot is kept instead of emptied.
- `uri` uses the item `url` field; when null or empty, it falls back to
  `https://news.ycombinator.com/item?id={id}`.
- `postedBy` uses `by`; when null, an empty string is returned.
- `time` is Unix seconds. When outside the range representable by `DateTimeOffset`,
  the item is discarded.
- `commentCount` uses `descendants`; when missing, it is `0`.
- Null items, items whose `type` is not `story`, and items without `title`/`score`
  are discarded.
- A single-attempt timeout (`TimeoutRejectedException`) neither triggers retries nor
  counts for the circuit breaker. This is a design decision: the timeout is the
  innermost strategy and is not part of the retry or circuit breaker `ShouldHandle`.
  Including it would let a slow HN open the circuit.
- `BaseUrl` may or may not end with a slash; the adapter always resolves relative
  paths under the configured base (`https://host/v0` and `https://host/v0/` are
  equivalent).
- The snapshot lives in the process memory. With several replicas, each keeps its
  own.
- The HN catalog is public: the endpoint requires no authentication.

#### Improvements

- Share the snapshot across replicas (for example, a distributed cache) for warm
  start and consistency.
- Incremental refresh instead of reloading the `MaxStories` items.
- HTTP caching with `ETag`/`Cache-Control` and response compression.
- Metrics and health checks: snapshot age, refresh duration, circuit breaker state.
- Handle `deleted`/`dead` and filter out withdrawn stories.
- Pagination and lookup by id.
