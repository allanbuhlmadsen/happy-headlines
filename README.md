# Happy Headlines — Semester Project

Architecture and implementation work for the Happy Headlines semester project.

## Repository layout

```
├── README.md
├── docs/
│   ├── workspace.dsl          C4 model (Structurizr DSL)
│   └── images/                Exported diagrams
└── src/
    └── HappyHeadlines/
        ├── HappyHeadlines.slnx
        ├── docker-compose.yml
        ├── ArticleService/    Articles (Week 2)
        ├── CommentService/    Comments (Week 3)
        ├── ProfanityService/  Profanity filtering (Week 3)
        └── nginx/             Load balancer configuration
```

---

# Week 1 — C4 model, levels 1 and 2

A C4 model of the system described in the assignment, covering the context level
and the container level. No implementation.

## Diagrams

| Level | Diagram | Key / legend |
|---|---|---|
| 1 — System Context | `docs/images/Level1_Context.png` | `docs/images/Level1_Context-key.png` |
| 2 — Containers | `docs/images/Level2_Containers.png` | `docs/images/Level2_Containers-key.png` |

A third diagram, added in Week 2, is described under "Deployment" below. Levels 1
and 2 were extended with monitoring in Week 4, described under "Week 4 — Design
to be monitored" at the end of this file.

All three are generated from a single model in `docs/workspace.dsl`, written in
Structurizr DSL (a text format for describing C4 models). The key files explain
what the shapes and colours mean.

## How to view or regenerate

Online, without installing anything:

1. Open <https://structurizr.com/dsl>
2. Clear the editor and paste the contents of `docs/workspace.dsl`
3. Generate, then switch between the three views

Note that the Structurizr cloud service, including this online editor and the
`theme default` used by the workspace, reaches its end of life on 30 September 2026.
After that date use the local option below.

Locally with Docker:

```
docker run -it --rm -p 8080:8080 -v /path/to/docs:/usr/local/structurizr structurizr/structurizr local
```

Then open <http://localhost:8080>. Note that this uses the same port as the
Week 2 load balancer, so do not run both at once.

## Model overview

Three people use the system: the **Publisher**, who drafts and publishes articles,
the **Reader**, who reads articles, comments, and subscribes to the newsletter, and
the **Operator**, the developer on call who watches the monitoring dashboard.

The system contains 17 containers:

- **Front ends (2)** — Webapp, Website
- **Services (7)** — DraftService, PublisherService, ProfanityService,
  ArticleService, CommentService, SubscriberService, NewsletterService
- **Queues (2)** — ArticleQueue, SubscriberQueue
- **Databases (5)** — DraftDatabase, ArticleDatabase, CommentDatabase,
  ProfanityDatabase, SubscriberDatabase
- **Monitoring (1)** — TelemetryCollector

Two external systems: an **Email System** that delivers the newsletter, and a
**Monitoring System** that stores and shows metrics, logs, and traces.

## Notation

- Rectangles are containers; cylinders are databases; pipes are queues
- Grey elements sit outside the system boundary
- Arrows point from the initiator of an interaction, which is not always the
  direction data flows — the Website calls ArticleService, though articles come back
- Queues have no arrow passing through them; the producer and consumer do not
  know about each other, which is the point of using a queue

## Assumptions and open questions

The assignment description leaves a few things unspecified. Rather than filling
the gaps silently, they are documented here.

### 1. The Email System is modelled as external

The description states that the NewsletterService sends the newsletter to all
active subscribers, but does not say how delivery happens. We assume an external
email provider, since delivering to the stated subscriber base requires
infrastructure outside the system boundary. It could alternatively be modelled as
a container inside the system if the company ran its own mail infrastructure.

### 2. NewsletterService reads from SubscriberQueue

The description does not say who consumes SubscriberQueue. We model
NewsletterService as the consumer, because a queue with no consumer serves no
purpose, and NewsletterService is the only service that works with subscribers.

Note that this gives two paths to subscriber data. They serve different purposes:
the queue carries individual new sign-ups as they happen, while the direct call to
SubscriberService retrieves all active subscribers when the newsletter is sent.
What NewsletterService actually does with a new sign-up is not stated — a welcome
email is the most likely intent, but that is our interpretation.

### 3. System-level relationships are written out by hand

By default Structurizr derives level 1 relationships from the level 2 ones, but it
creates only one per pair of elements. The Reader interacts with the Website in
three ways, so the derived version showed only the first of them — a context
diagram implying the Reader does nothing but read.

Derivation is therefore switched off (`!impliedRelationships false`), and all
level 1 relationships are stated explicitly in the model. This means level 1 and
level 2 are maintained separately: a new interaction added at the container level
will not appear on the context diagram unless it is added there too.

That trade-off is deliberate. The context diagram is meant to summarise at a level
of abstraction the container diagram cannot express, and a per-container derivation
does not summarise well.

### 4. No link between DraftService and PublisherService

The Webapp talks to both services, but the description does not connect them to
each other. We assume the Webapp sends the article content to PublisherService
directly, rather than PublisherService fetching the draft. This keeps the two
services independent, consistent with the rest of the system.

### 5. PublisherService and NewsletterService have no database

Five of the seven services own exactly one database each. PublisherService and
NewsletterService own none, because the description gives them none. Both are
coordinating services that move data between other containers without holding
state of their own, so this is defensible — but it is likely a gap in the
description rather than a deliberate choice. A production system would probably
want a publication audit trail and a record of which newsletters were sent to
whom.

### 6. No technologies specified

Container technology fields are left empty. The description names no languages,
frameworks, or database engines, so filling them in would be invention.

The one exception is TelemetryCollector, added in Week 4. It is an existing
product rather than something we build, so its technology is known and stated.

---

# Week 2 — ArticleService, X-axis and Z-axis splits

Three requirements: a REST-based ArticleService with Create, Read, Update and
Delete endpoints; an X-axis split giving three service instances behind a load
balancer; and a Z-axis split giving each continent its own database plus a global
one.

## Stack

- ASP.NET Core Web API on .NET 10, written in C#
- PostgreSQL 17, one server per continent
- Entity Framework Core with Npgsql
- Nginx as the load balancer
- Docker Compose to run all twelve containers

## How to run

From `src/HappyHeadlines`:

```
docker compose up --scale articleservice=3
```

The `--scale` flag is required. Without it only one service instance runs and the
X-axis split is not exercised.

The API is then available on <http://localhost:8080>. Nothing else is exposed to
the host: the databases and the service instances are reachable only on the
internal Docker network.

To reset all data:

```
docker compose down -v
```

## API

All endpoints are scoped to a continent:

| Method | Route | Action |
|---|---|---|
| `GET` | `/continents/{continent}/articles` | List all articles in that continent |
| `GET` | `/continents/{continent}/articles/{id}` | Read one article |
| `POST` | `/continents/{continent}/articles` | Create an article |
| `PUT` | `/continents/{continent}/articles/{id}` | Update an article |
| `DELETE` | `/continents/{continent}/articles/{id}` | Delete an article |

Valid continents: `africa`, `antarctica`, `asia`, `europe`, `northamerica`,
`oceania`, `southamerica`, `global`. Any other value returns 404.

`ArticleService/ArticleService.http` contains ready-made requests for all of them.

## The two splits

**X-axis** — three identical instances of ArticleService run behind Nginx, which
distributes requests round robin. The instances share nothing: any of them can
serve any request, because all state lives in the databases.

**Z-axis** — eight separate PostgreSQL servers, one per continent plus a global
one. The continent is taken from the route, which is what makes the split work:
the service knows which database to use before it looks anything up.

## Deployment

`docs/images/Deployment.png` (legend: `docs/images/Deployment-key.png`) shows how
ArticleService is actually run: the load balancer, the three service instances,
and the eight database containers.

The container diagram is deliberately unchanged. It shows what the system is made
of — one ArticleService, one ArticleDatabase — which is still accurate: three
instances of the same service are not three different containers, and eight
databases with the same schema are one container deployed eight times. How many
copies run, and on what infrastructure, is what a deployment diagram is for.

Keeping the two separate is also what makes the diagrams survive the rest of the
semester. Drawing every service's instances on the container diagram would make it
unreadable by the time the other six services exist.

## Design decisions

### 1. The continent is part of the route

Alternatives considered were storing the continent on the article and looking it
up, or encoding it in the article id.

Looking it up would mean either querying all eight databases or maintaining a
central registry — the first defeats the purpose of the split, the second
reintroduces the bottleneck it was meant to remove. Encoding it in the id works,
but puts meaning into an identifier, so an article that changes continent would
need a new id and every existing reference to it would break.

Putting the continent in the route means the caller must know it, which is a real
cost. In exchange, every request reaches exactly one database, and the split is
visible in the API rather than hidden in the implementation.

### 2. Global is the eighth continent, not a special case

The assignment calls it "an eighth database". Treating it as one more valid value
rather than a special case means the routing logic is a single lookup with eight
entries, with no exception to maintain at every point where a database is chosen.

### 3. Cross-continent reads are not implemented

A reader in Europe would presumably want European and global articles together,
which means querying two databases and merging.

This is deliberately left out. The assignment asks for four endpoints, each of
which reaches one database. The merging rule is really a presentation concern, and
the Website that would need it has not been built yet — implementing it now would
mean guessing at a requirement we have not seen.

When it is needed, the intended approach is for the caller to make two requests and
merge, rather than having ArticleService do it. That keeps every service call
scoped to a single database, which is what the Z-axis split is for.

### 4. Eight database servers, not one server with eight databases

PostgreSQL can host several databases in one server process, which would have been
much lighter to run. It would not have been a Z-axis split: all eight would share
a process, a machine, and its memory, so load and failures would not be isolated.
Since isolation is the point of the split, each continent gets its own server.

### 5. PostgreSQL rather than SQL Server

Eight SQL Server containers are not realistic on a laptop — the image is over 1.5 GB
and Microsoft recommends at least 2 GB of memory per instance. PostgreSQL is a
fraction of that. Without the Z-axis split the choice would not matter.

### 6. Entity Framework Core rather than Dapper

Dapper would have made the split more explicit — a connection is just a string you
pass in, so eight databases are eight strings. Entity Framework Core is built
around a single database, so selecting one per request has to be arranged
deliberately.

Entity Framework Core was chosen anyway because it is what the course covers.
Learning a second data access library on top of the assignment was not a good
trade for one week's work.

### 7. Database selection is isolated in one class

`ContinentDbContextFactory` knows the eight continent names, validates them, and
produces a context for a given continent. The five endpoints ask it for a context
and never see a connection string.

The alternative was to have each endpoint build its own connection. That would put
the same translation and the same validation in five places, each of which could
drift or be forgotten. It also makes the split harder to explain: with one class
there is a single place to point at.

### 8. The continent is stored on the article, but derived

Storing it duplicates what the database location already implies. The risk of
duplication is contradiction — an article in `db-asia` claiming to be European.

That risk only exists if the caller can set the field. The service sets it from the
route and ignores anything sent in the request body, so the two cannot disagree.
With that constraint in place the field pays for itself: it makes future merging
possible, makes routing bugs visible in the data, and makes a database backup
self-describing.

The same applies to `Id`, which is assigned by the database. `Update` copies only
the caller-editable fields for the same reason.

### 9. Migrations run at service startup

Each instance runs `Migrate()` against all eight databases when it starts. The
alternative — running `dotnet ef database update` by hand — would mean eight
invocations, easily leaving one database behind.

With three instances starting at once, all three attempt to migrate. Entity
Framework Core takes an exclusive lock on the migrations history table, so the
first one applies them and the others find nothing to do. This is safe here, but
running migrations from application startup is not generally advisable in
production, where a separate deployment step is preferred.

### 10. Health checks, not just `depends_on`

`depends_on` waits for a container to start, not to be ready. Eight PostgreSQL
servers initialising for the first time take long enough that the service reached
them first and crashed. Each database therefore has a health check using
`pg_isready`, and the service waits for `service_healthy`.

### 11. Round robin

Nginx's default. The three instances are identical and hold no local state, so
there is no reason to prefer one over another. IP hash would tie a client to one
instance, which only makes sense if instances keep state — and would work against
the point of the X-axis split.

### 12. No HTTPS inside the compose network

HTTPS was disabled on the service. Certificates inside containers would have to be
managed for three instances, and the usual arrangement is to terminate encryption
at the edge and run plaintext within a closed network. Nothing but the load
balancer is exposed to the host.

### 13. The article has six fields

The assignment does not describe an article at all. The fields are `Id`, `Title`,
`Body`, `Author`, `PublishedAt` and `Continent`. `PublishedAt` is not optional:
Week 1 states that the Website shows the most recent articles, which cannot be
ordered without it.

`Article` currently doubles as both the storage model and the request model. A
separate input model would be cleaner — the current version relies on the
controller overwriting `Id` and `Continent` rather than refusing to accept them.

## Known gaps (Week 2)

- The ArticleQueue subscription from the Week 1 model is not implemented; this
  week's requirements do not mention it
- The database password is committed in plain text. This is acceptable for a local
  development database with no real data, but is not a pattern to carry forward

---

# Week 3 — CommentService, ProfanityService, and a circuit breaker

Three requirements: implement CommentService and ProfanityService with their own
databases; isolate them from each other following swimlane principles, with the two
services communicating directly rather than through a gateway; and put a circuit
breaker in CommentService for when ProfanityService is unavailable.

## How to run

Unchanged from Week 2. From `src/HappyHeadlines`:

```
docker compose up --scale articleservice=3
```

Fourteen containers start: eleven databases, three ArticleService instances, the
load balancer, ProfanityService and CommentService.

| Service | Port on the host |
|---|---|
| ArticleService (via load balancer) | 8080 |
| ProfanityService | 8082 |
| CommentService | 8083 |

## API

**ProfanityService** — `/profanity`:

| Method | Route | Action |
|---|---|---|
| `POST` | `/profanity/filter` | Returns the text with prohibited words replaced |
| `GET` | `/profanity/words` | Lists the prohibited words |
| `POST` | `/profanity/words` | Adds a word |
| `DELETE` | `/profanity/words/{id}` | Removes a word |

**CommentService** — comments are scoped to an article:

| Method | Route | Action |
|---|---|---|
| `GET` | `/articles/{continent}/{articleId}/comments` | Lists comments on that article |
| `POST` | `/articles/{continent}/{articleId}/comments` | Posts a comment |
| `DELETE` | `/articles/{continent}/{articleId}/comments/{id}` | Deletes a comment |

## The tension in the assignment, and how it is resolved

Swimlane isolation normally means no synchronous calls between lanes: if
CommentService waits on ProfanityService, a hang in one hangs the other, which is
exactly the coupling lanes exist to remove.

The assignment nonetheless requires the two services to call each other directly,
with no gateway in between. That is not a contradiction — it is why the circuit
breaker is part of the same assignment. The breaker is what restores the isolation
that a synchronous call would otherwise destroy: after a few failures it stops
trying, fails fast, and recovers on its own.

What is isolated, concretely:

- Each service owns exactly one database; neither can reach the other's
- Neither service shares code, a process, or a container with the other
- The call goes to `http://profanityservice:8080` directly on the internal Docker
  network — not through the load balancer, which serves ArticleService only
- CommentService has no `depends_on` for ProfanityService: it must be able to start
  and run without it

## The circuit breaker

Implemented with `Microsoft.Extensions.Http.Resilience`, which builds on Polly, and
attached to the `HttpClient` that CommentService uses to reach ProfanityService.

| Setting | Value | Meaning |
|---|---|---|
| `FailureRatio` | 0.5 | Opens when half the calls in the window fail |
| `MinimumThroughput` | 4 | At least four calls before it counts at all |
| `SamplingDuration` | 30s | The window it looks back over |
| `BreakDuration` | 15s | How long it stays open before probing again |
| `Timeout` | 5s | How long a single call may take |

The values are deliberately low so the behaviour can be demonstrated without
waiting minutes.

### Measured behaviour

With ProfanityService stopped:

| | Response time | Status |
|---|---|---|
| Before the breaker opens | ~1.4 s | 503 |
| After the breaker opens | 2–3 ms | 503 |

The difference is the point. Once open, CommentService stops attempting a call it
knows will fail, so it neither waits nor holds a connection open. Bringing
ProfanityService back requires no intervention: after `BreakDuration` the breaker
probes, succeeds, and closes itself.

## Design decisions

### 1. When the breaker is open, the comment is rejected

Four options were considered: reject the comment, accept it unfiltered, store it
unpublished until it can be filtered, or fall back to a local word list.

Accepting unfiltered would let profanity onto a site whose entire premise is
positive news, and would make the breaker pointless — if the answer is "do nothing",
the call could simply fail silently. A local fallback list would put filtering logic
in two places, which is what ProfanityService exists to prevent. Storing unpublished
comments is the right answer for production, but requires a background process to
filter them later, which this week's requirements do not ask for.

Rejecting is honest and simple, and it is what the breaker is actually for. The
response is 503 with a message saying to try again shortly, rather than a bare 500,
so it is a communicated decision rather than a crash.

The trade-off is real and worth naming: this isolates resource consumption, not the
user experience. A reader still cannot comment while ProfanityService is down.
Storing unpublished comments would fix that, and is the intended next step.

### 2. ProfanityService filters text rather than handing out the word list

The alternative was an endpoint returning the prohibited words, with CommentService
doing the matching. That would put the filtering logic in the caller and leave
ProfanityService as a database with a URL in front of it — the swimlane would carry
no responsibility.

The Week 1 description also has PublisherService using ProfanityService. With
filtering inside the service, that logic exists once rather than in every caller.

### 3. Whole words only, replaced with asterisks

`damn` is matched; `damned` is not. Substring matching would also catch `ass` inside
`classic` and `passage` — the classic failure of naive profanity filters. Fewer
matches is the better failure.

Matches are replaced with one asterisk per letter rather than removed, so the
sentence keeps its shape and the censoring is visible. Matching ignores case; words
are stored lowercase.

### 4. Comments store the filtered text, not the original

The original wording cannot be recovered. Storing both would mean keeping the
profanity in the database, which defeats the purpose of filtering it.

### 5. A comment identifies its article by continent and id

Article ids are only unique within one continent's database, a consequence of the
Week 2 Z-axis split. A comment therefore stores both `ArticleContinent` and
`ArticleId`; either alone is ambiguous.

The assignment does not mention this. It follows from the previous week's design.

### 6. The two services duplicate the request and response types

`FilterRequest` and `FilterResponse` are defined separately in each service rather
than shared through a common library. The duplication is deliberate: a shared
library would couple the two services at build time, which works against the
isolation the swimlanes are meant to provide.

### 7. Neither new service is split on the X or Z axis

The assignment asks for fault isolation, not scaling, so each runs as a single
instance with a single database. Adding eight comment databases would obscure what
is actually being assessed.

This means the compose file now contains two patterns side by side: ArticleService
scaled and partitioned, the two new services neither. That is a deliberate
difference, not an oversight.

If CommentService is scaled later, note that each instance keeps its own circuit
breaker state in memory. They would open independently and discover an outage
separately. That is the usual arrangement, but it is worth knowing.

### 8. ProfanityService exposes a host port

Port 8082 is published so the word list can be maintained and the filter tested
during development. This does not weaken the isolation — CommentService still calls
the service directly on the internal network regardless — but the port is not
required by the design and could be removed.

## Known gaps (Week 3)

- Comments rejected while the breaker is open are lost; the caller has to resend
- There is no authentication on the word-list endpoints
- The word list is fetched from the database on every filter call, with no caching

---

# Week 4 — Design to be monitored

The C4 model from Week 1 is extended so the system can be monitored: every
service must be able to report metrics, logs, and traces, and someone must be able
to see them and be alerted when something goes wrong.

## What was added

**Level 1 — System Context**

- **Operator** (person): the developer on call who watches the dashboard and
  handles incidents
- **Monitoring System** (external software system): stores metrics, logs, and
  traces, shows the dashboard, and sends alerts
- Happy Headlines sends metrics, logs, and traces to the Monitoring System; the
  Operator watches the dashboard in it, and it alerts the Operator about incidents

**Level 2 — Containers**

- **TelemetryCollector** (container): collects metrics, logs, and traces from all
  services and forwards them to the Monitoring System
- All nine front ends and services send their metrics, logs, and traces to
  TelemetryCollector
- The Operator and the Monitoring System are shown outside the system boundary

The deployment diagram is unchanged.

## Design decisions

### 1. Buy, do not build

Monitoring is not what Happy Headlines exists to do, so it is built from existing
products rather than written by us. Both new elements say so in their description.

- **Monitoring System** — the Grafana stack, which can store metrics, logs, and
  traces, show them on a dashboard, and alert when a value crosses a limit
- **TelemetryCollector** — the OpenTelemetry Collector, which receives all three
  kinds of data and forwards them

### 2. One collector inside the system, not one arrow per service to the outside

Each service knows a single address, the collector's. Where the data ends up is
decided in one place, so the Monitoring System can be replaced without touching the
services. It also keeps level 1 readable: Happy Headlines has one relationship to
the Monitoring System rather than one per service.

### 3. The Monitoring System is external

It sits outside the system boundary, like the Email System, because it is a
separate product that Happy Headlines only uses.

### 4. Databases and queues have no arrow to the collector

Only the front ends and services send data themselves. The figures for databases
and queues are fetched by the monitoring tools, so drawing arrows from them would
suggest those containers do work they do not do.

### 5. The Operator is included explicitly

`include *` only shows people with a relationship to the element in focus. The
Operator only interacts with the Monitoring System, so both views contain
`include operator`; without it the Operator would be missing from both diagrams.

## Known gaps (Week 4)

- The monitoring is modelled, not implemented; no service sends data yet
- The container diagram has many crossing arrows now that nine containers point to
  TelemetryCollector
