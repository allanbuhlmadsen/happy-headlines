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
        ├── DraftService/      Drafts, with logging and tracing (Week 4)
        ├── Observability/     Shared Serilog and OpenTelemetry setup (Week 4)
        ├── PublisherService/  Publishing, onto the queue (Week 5)
        ├── NewsletterService/ Newsletter, off the queue (Week 5)
        ├── Webapp/            Editorial front end, Razor Pages (Week 5)
        ├── nginx/             Load balancer configuration
        ├── prometheus/        Metrics collection (Week 6)
        └── grafana/           Dashboard, provisioned from files (Week 6)
```

---

# Week 1 — C4 model, levels 1 and 2

A C4 model of the system described in the assignment, covering the context level and the container level. No implementation.

## Diagrams

| Level | Diagram | Key / legend |
|---|---|---|
| 1 — System Context | `docs/images/Level1_Context.png` | `docs/images/Level1_Context-key.png` |
| 2 — Containers | `docs/images/Level2_Containers.png` | `docs/images/Level2_Containers-key.png` |
| 2 — Monitoring | `docs/images/Level2_Monitoring.png` | `docs/images/Level2_Monitoring-key.png` |

A third diagram, added in Week 2, is described under "Deployment" below. Levels 1 and 2 were extended with logging and tracing in Week 4, described under "Week 4 — Design to be monitored" at the end of this file.

All three are generated from a single model in `docs/workspace.dsl`, written in Structurizr DSL (a text format for describing C4 models). The key files explain what the shapes and colours mean.

## How to view or regenerate

Online, without installing anything:

1. Open <https://structurizr.com/dsl>
2. Clear the editor and paste the contents of `docs/workspace.dsl`
3. Generate, then switch between the three views

Note that the Structurizr cloud service, including this online editor and the `theme default` used by the workspace, reaches its end of life on 30 September 2026. After that date use the local option below.

Locally with Docker:

```
docker run -it --rm -p 8080:8080 -v /path/to/docs:/usr/local/structurizr structurizr/structurizr local
```

Then open <http://localhost:8080>. Note that this uses the same port as the Week 2 load balancer, so do not run both at once.

## Model overview

Three people use the system: the **Publisher**, who drafts and publishes articles, the **Reader**, who reads articles, comments, and subscribes to the newsletter, and the **Operator**, the developer on call who reads the log entries and traces.

The system contains 16 containers:

- **Front ends (2)** — Webapp, Website
- **Services (7)** — DraftService, PublisherService, ProfanityService, ArticleService, CommentService, SubscriberService, NewsletterService
- **Queues (2)** — ArticleQueue, SubscriberQueue
- **Databases (5)** — DraftDatabase, ArticleDatabase, CommentDatabase, ProfanityDatabase, SubscriberDatabase

Three external systems: an **Email System** that delivers the newsletter, **Seq**, which stores and shows log entries, and **Zipkin**, which stores and shows traces.

## Notation

- Rectangles are containers; cylinders are databases; pipes are queues
- Grey elements sit outside the system boundary
- Arrows point from the initiator of an interaction, which is not always the direction data flows — the Website calls ArticleService, though articles come back
- Queues have no arrow passing through them; the producer and consumer do not know about each other, which is the point of using a queue

## Assumptions and open questions

The assignment description leaves a few things unspecified. Rather than filling the gaps silently, they are documented here.

### 1. The Email System is modelled as external

The description states that the NewsletterService sends the newsletter to all active subscribers, but does not say how delivery happens. We assume an external email provider, since delivering to the stated subscriber base requires infrastructure outside the system boundary. It could alternatively be modelled as a container inside the system if the company ran its own mail infrastructure.

### 2. NewsletterService reads from SubscriberQueue

The description does not say who consumes SubscriberQueue. We model NewsletterService as the consumer, because a queue with no consumer serves no purpose, and NewsletterService is the only service that works with subscribers.

Note that this gives two paths to subscriber data. They serve different purposes: the queue carries individual new sign-ups as they happen, while the direct call to SubscriberService retrieves all active subscribers when the newsletter is sent. What NewsletterService actually does with a new sign-up is not stated — a welcome email is the most likely intent, but that is our interpretation.

### 3. System-level relationships are written out by hand

By default Structurizr derives level 1 relationships from the level 2 ones, but it creates only one per pair of elements. The Reader interacts with the Website in three ways, so the derived version showed only the first of them — a context diagram implying the Reader does nothing but read.

Derivation is therefore switched off (`!impliedRelationships false`), and all level 1 relationships are stated explicitly in the model. This means level 1 and level 2 are maintained separately: a new interaction added at the container level will not appear on the context diagram unless it is added there too.

That trade-off is deliberate. The context diagram is meant to summarise at a level of abstraction the container diagram cannot express, and a per-container derivation does not summarise well.

### 4. No link between DraftService and PublisherService

The Webapp talks to both services, but the description does not connect them to each other. We assume the Webapp sends the article content to PublisherService directly, rather than PublisherService fetching the draft. This keeps the two services independent, consistent with the rest of the system.

### 5. PublisherService and NewsletterService have no database

Five of the seven services own exactly one database each. PublisherService and NewsletterService own none, because the description gives them none. Both are coordinating services that move data between other containers without holding state of their own, so this is defensible — but it is likely a gap in the description rather than a deliberate choice. A production system would probably want a publication audit trail and a record of which newsletters were sent to whom.

### 6. No technologies specified

Container technology fields are left empty. The description names no languages, frameworks, or database engines, so filling them in would be invention.

Week 4 adds the first exceptions. DraftService is built this week, so its technology (REST API) is stated, and so is the technology of the Observability library it uses.

---

# Week 2 — ArticleService, X-axis and Z-axis splits

Three requirements: a REST-based ArticleService with Create, Read, Update and Delete endpoints; an X-axis split giving three service instances behind a load balancer; and a Z-axis split giving each continent its own database plus a global one.

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

The `--scale` flag is required. Without it only one service instance runs and the X-axis split is not exercised.

The API is then available on <http://localhost:8080>. Nothing else is exposed to the host: the databases and the service instances are reachable only on the internal Docker network.

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

Valid continents: `africa`, `antarctica`, `asia`, `europe`, `northamerica`, `oceania`, `southamerica`, `global`. Any other value returns 404.

`ArticleService/ArticleService.http` contains ready-made requests for all of them.

## The two splits

**X-axis** — three identical instances of ArticleService run behind Nginx, which distributes requests round robin. The instances share nothing: any of them can serve any request, because all state lives in the databases.

**Z-axis** — eight separate PostgreSQL servers, one per continent plus a global one. The continent is taken from the route, which is what makes the split work: the service knows which database to use before it looks anything up.

## Deployment

`docs/images/Deployment.png` (legend: `docs/images/Deployment-key.png`) shows how ArticleService is actually run: the load balancer, the three service instances, and the eight database containers.

The container diagram is deliberately unchanged. It shows what the system is made of — one ArticleService, one ArticleDatabase — which is still accurate: three instances of the same service are not three different containers, and eight databases with the same schema are one container deployed eight times. How many copies run, and on what infrastructure, is what a deployment diagram is for.

Keeping the two separate is also what makes the diagrams survive the rest of the semester. Drawing every service's instances on the container diagram would make it unreadable by the time the other six services exist.

## Design decisions

### 1. The continent is part of the route

Alternatives considered were storing the continent on the article and looking it up, or encoding it in the article id.

Looking it up would mean either querying all eight databases or maintaining a central registry — the first defeats the purpose of the split, the second reintroduces the bottleneck it was meant to remove. Encoding it in the id works, but puts meaning into an identifier, so an article that changes continent would need a new id and every existing reference to it would break.

Putting the continent in the route means the caller must know it, which is a real cost. In exchange, every request reaches exactly one database, and the split is visible in the API rather than hidden in the implementation.

### 2. Global is the eighth continent, not a special case

The assignment calls it "an eighth database". Treating it as one more valid value rather than a special case means the routing logic is a single lookup with eight entries, with no exception to maintain at every point where a database is chosen.

### 3. Cross-continent reads are not implemented

A reader in Europe would presumably want European and global articles together, which means querying two databases and merging.

This is deliberately left out. The assignment asks for four endpoints, each of which reaches one database. The merging rule is really a presentation concern, and the Website that would need it has not been built yet — implementing it now would mean guessing at a requirement we have not seen.

When it is needed, the intended approach is for the caller to make two requests and merge, rather than having ArticleService do it. That keeps every service call scoped to a single database, which is what the Z-axis split is for.

### 4. Eight database servers, not one server with eight databases

PostgreSQL can host several databases in one server process, which would have been much lighter to run. It would not have been a Z-axis split: all eight would share a process, a machine, and its memory, so load and failures would not be isolated. Since isolation is the point of the split, each continent gets its own server.

### 5. PostgreSQL rather than SQL Server

Eight SQL Server containers are not realistic on a laptop — the image is over 1.5 GB and Microsoft recommends at least 2 GB of memory per instance. PostgreSQL is a fraction of that. Without the Z-axis split the choice would not matter.

### 6. Entity Framework Core rather than Dapper

Dapper would have made the split more explicit — a connection is just a string you pass in, so eight databases are eight strings. Entity Framework Core is built around a single database, so selecting one per request has to be arranged deliberately.

Entity Framework Core was chosen anyway because it is what the course covers. Learning a second data access library on top of the assignment was not a good trade for one week's work.

### 7. Database selection is isolated in one class

`ContinentDbContextFactory` knows the eight continent names, validates them, and produces a context for a given continent. The five endpoints ask it for a context and never see a connection string.

The alternative was to have each endpoint build its own connection. That would put the same translation and the same validation in five places, each of which could drift or be forgotten. It also makes the split harder to explain: with one class there is a single place to point at.

### 8. The continent is stored on the article, but derived

Storing it duplicates what the database location already implies. The risk of duplication is contradiction — an article in `db-asia` claiming to be European.

That risk only exists if the caller can set the field. The service sets it from the route and ignores anything sent in the request body, so the two cannot disagree. With that constraint in place the field pays for itself: it makes future merging possible, makes routing bugs visible in the data, and makes a database backup self-describing.

The same applies to `Id`, which is assigned by the database. `Update` copies only the caller-editable fields for the same reason.

### 9. Migrations run at service startup

Each instance runs `Migrate()` against all eight databases when it starts. The alternative — running `dotnet ef database update` by hand — would mean eight invocations, easily leaving one database behind.

With three instances starting at once, all three attempt to migrate. Entity Framework Core takes an exclusive lock on the migrations history table, so the first one applies them and the others find nothing to do. This is safe here, but running migrations from application startup is not generally advisable in production, where a separate deployment step is preferred.

### 10. Health checks, not just `depends_on`

`depends_on` waits for a container to start, not to be ready. Eight PostgreSQL servers initialising for the first time take long enough that the service reached them first and crashed. Each database therefore has a health check using `pg_isready`, and the service waits for `service_healthy`.

### 11. Round robin

Nginx's default. The three instances are identical and hold no local state, so there is no reason to prefer one over another. IP hash would tie a client to one instance, which only makes sense if instances keep state — and would work against the point of the X-axis split.

### 12. No HTTPS inside the compose network

HTTPS was disabled on the service. Certificates inside containers would have to be managed for three instances, and the usual arrangement is to terminate encryption at the edge and run plaintext within a closed network. Nothing but the load balancer is exposed to the host.

### 13. The article has six fields

The assignment does not describe an article at all. The fields are `Id`, `Title`, `Body`, `Author`, `PublishedAt` and `Continent`. `PublishedAt` is not optional: Week 1 states that the Website shows the most recent articles, which cannot be ordered without it.

`Article` currently doubles as both the storage model and the request model. A separate input model would be cleaner — the current version relies on the controller overwriting `Id` and `Continent` rather than refusing to accept them.

## Known gaps (Week 2)

- The ArticleQueue subscription from the Week 1 model is not implemented; this week's requirements do not mention it
- The database password is committed in plain text. This is acceptable for a local development database with no real data, but is not a pattern to carry forward

---

# Week 3 — CommentService, ProfanityService, and a circuit breaker

Three requirements: implement CommentService and ProfanityService with their own databases; isolate them from each other following swimlane principles, with the two services communicating directly rather than through a gateway; and put a circuit breaker in CommentService for when ProfanityService is unavailable.

## How to run

Unchanged from Week 2. From `src/HappyHeadlines`:

```
docker compose up --scale articleservice=3
```

Fourteen containers start: eleven databases, three ArticleService instances, the load balancer, ProfanityService and CommentService.

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

Swimlane isolation normally means no synchronous calls between lanes: if CommentService waits on ProfanityService, a hang in one hangs the other, which is exactly the coupling lanes exist to remove.

The assignment nonetheless requires the two services to call each other directly, with no gateway in between. That is not a contradiction — it is why the circuit breaker is part of the same assignment. The breaker is what restores the isolation that a synchronous call would otherwise destroy: after a few failures it stops trying, fails fast, and recovers on its own.

What is isolated, concretely:

- Each service owns exactly one database; neither can reach the other's
- Neither service shares code, a process, or a container with the other
- The call goes to `http://profanityservice:8080` directly on the internal Docker network — not through the load balancer, which serves ArticleService only
- CommentService has no `depends_on` for ProfanityService: it must be able to start and run without it

## The circuit breaker

Implemented with `Microsoft.Extensions.Http.Resilience`, which builds on Polly, and attached to the `HttpClient` that CommentService uses to reach ProfanityService.

| Setting | Value | Meaning |
|---|---|---|
| `FailureRatio` | 0.5 | Opens when half the calls in the window fail |
| `MinimumThroughput` | 4 | At least four calls before it counts at all |
| `SamplingDuration` | 30s | The window it looks back over |
| `BreakDuration` | 15s | How long it stays open before probing again |
| `Timeout` | 5s | How long a single call may take |

The values are deliberately low so the behaviour can be demonstrated without waiting minutes.

### Measured behaviour

With ProfanityService stopped:

| | Response time | Status |
|---|---|---|
| Before the breaker opens | ~1.4 s | 503 |
| After the breaker opens | 2–3 ms | 503 |

The difference is the point. Once open, CommentService stops attempting a call it knows will fail, so it neither waits nor holds a connection open. Bringing ProfanityService back requires no intervention: after `BreakDuration` the breaker probes, succeeds, and closes itself.

## Design decisions

### 1. When the breaker is open, the comment is rejected

Four options were considered: reject the comment, accept it unfiltered, store it unpublished until it can be filtered, or fall back to a local word list.

Accepting unfiltered would let profanity onto a site whose entire premise is positive news, and would make the breaker pointless — if the answer is "do nothing", the call could simply fail silently. A local fallback list would put filtering logic in two places, which is what ProfanityService exists to prevent. Storing unpublished comments is the right answer for production, but requires a background process to filter them later, which this week's requirements do not ask for.

Rejecting is honest and simple, and it is what the breaker is actually for. The response is 503 with a message saying to try again shortly, rather than a bare 500, so it is a communicated decision rather than a crash.

The trade-off is real and worth naming: this isolates resource consumption, not the user experience. A reader still cannot comment while ProfanityService is down. Storing unpublished comments would fix that, and is the intended next step.

### 2. ProfanityService filters text rather than handing out the word list

The alternative was an endpoint returning the prohibited words, with CommentService doing the matching. That would put the filtering logic in the caller and leave ProfanityService as a database with a URL in front of it — the swimlane would carry no responsibility.

The Week 1 description also has PublisherService using ProfanityService. With filtering inside the service, that logic exists once rather than in every caller.

### 3. Whole words only, replaced with asterisks

`damn` is matched; `damned` is not. Substring matching would also catch `ass` inside `classic` and `passage` — the classic failure of naive profanity filters. Fewer matches is the better failure.

Matches are replaced with one asterisk per letter rather than removed, so the sentence keeps its shape and the censoring is visible. Matching ignores case; words are stored lowercase.

### 4. Comments store the filtered text, not the original

The original wording cannot be recovered. Storing both would mean keeping the profanity in the database, which defeats the purpose of filtering it.

### 5. A comment identifies its article by continent and id

Article ids are only unique within one continent's database, a consequence of the Week 2 Z-axis split. A comment therefore stores both `ArticleContinent` and `ArticleId`; either alone is ambiguous.

The assignment does not mention this. It follows from the previous week's design.

### 6. The two services duplicate the request and response types

`FilterRequest` and `FilterResponse` are defined separately in each service rather than shared through a common library. The duplication is deliberate: a shared library would couple the two services at build time, which works against the isolation the swimlanes are meant to provide.

### 7. Neither new service is split on the X or Z axis

The assignment asks for fault isolation, not scaling, so each runs as a single instance with a single database. Adding eight comment databases would obscure what is actually being assessed.

This means the compose file now contains two patterns side by side: ArticleService scaled and partitioned, the two new services neither. That is a deliberate difference, not an oversight.

If CommentService is scaled later, note that each instance keeps its own circuit breaker state in memory. They would open independently and discover an outage separately. That is the usual arrangement, but it is worth knowing.

### 8. ProfanityService exposes a host port

Port 8082 is published so the word list can be maintained and the filter tested during development. This does not weaken the isolation — CommentService still calls the service directly on the internal network regardless — but the port is not required by the design and could be removed.

## Known gaps (Week 3)

- Comments rejected while the breaker is open are lost; the caller has to resend
- There is no authentication on the word-list endpoints
- The word list is fetched from the database on every filter call, with no caching

---

# Week 4 — Design to be monitored

Two parts this week. The C4 model is extended so the system can be monitored, and DraftService and DraftDatabase are built with logging and tracing from the start.

## Part 1 — The C4 model

**Level 1 — System Context**

- **Operator** (person): the developer on call who reads the log entries and traces and handles incidents
- **Seq** (external software system): stores the structured log entries and shows them in a browser
- **Zipkin** (external software system): stores the traces and shows each request as a timeline
- Happy Headlines sends log entries to Seq and traces to Zipkin; the Operator reads them in each of the two

**Level 2 — Containers**

- **DraftService** sends its log entries to Seq and its traces to Zipkin
- The Operator, Seq and Zipkin are shown outside the system boundary

The deployment diagram is unchanged.

## Part 2 — DraftService and DraftDatabase

**DraftService** is a REST API with five endpoints:

| Method and path | What it does |
| --- | --- |
| `GET /drafts` | All drafts, most recently updated first |
| `GET /drafts/{id}` | One draft |
| `POST /drafts` | Creates a draft |
| `PUT /drafts/{id}` | Updates a draft |
| `DELETE /drafts/{id}` | Deletes a draft |

A draft has a title, a body, an author, and the two timestamps `CreatedAt` and `UpdatedAt`, which the service sets itself. **DraftDatabase** is a PostgreSQL 17 container named `db-drafts`, with the same health check as the other databases, so DraftService waits until it is ready.

**Observability** is a class library shared by the services. A service gets logging and tracing by calling `builder.AddObservability("<service name>")` and `app.UseObservability()` in its `Program.cs`, and nothing else.

| Tool | Role |
| --- | --- |
| Serilog | Writes structured log entries in the services |
| Seq | Stores the log entries and shows them, on port 5341 |
| OpenTelemetry | Produces traces in the services |
| Zipkin | Stores the traces and shows them, on port 9411 |

## Design decisions

### 1. Buy, do not build

Monitoring is not what Happy Headlines exists to do, so the storage and the user interfaces are existing products: Seq for log entries, Zipkin for traces. Both say so in their description in the model. Only the setup that connects the services to them is written by us.

### 2. Logs and traces go to separate products

Serilog and Seq belong together, and so do OpenTelemetry and Zipkin, so each type of data goes where its tooling is strongest. The two are tied together instead: every log entry carries the `TraceId` and `SpanId` that OpenTelemetry assigned to the request, so a log entry in Seq can be looked up as a trace in Zipkin.

### 3. Shared infrastructure code, not shared domain code

Week 3, decision 6 deliberately avoided shared code between services. The Observability library is an exception, and a narrow one: it contains setup only — no business logic and no data types that two services could come to depend on each other through. The alternative is the same setup copied into every service, where changing where the data is sent means editing all of them. The assignment also asks for something reusable across the architecture.

### 4. What is logged, and when

| Level | Used for | Example |
| --- | --- | --- |
| Information | Something succeeded that is worth knowing afterwards | Draft 12 was created |
| Warning | The call failed because of the caller, not the system | Draft 99 does not exist |
| Error | The system could not do its work | The database does not answer |

Debug is switched off. ASP.NET Core and Entity Framework Core are turned down to warnings, since they log a great deal about themselves.

The body of a draft is never logged. It can be long, and a draft is by definition unpublished, so it does not belong in a log that many people can read. The HTTP request itself is not logged in the controller either: `UseSerilogRequestLogging()` already writes one entry per request with method, path, status code and duration, and the trace covers the same ground.

### 5. The controller does not know Serilog

The controller takes the `ILogger<DraftsController>` that .NET provides, through dependency injection. Only the Observability library knows that the entries end up in Seq, so replacing the logging tool does not touch any controller.

### 6. Database tracing comes from Npgsql itself

`OpenTelemetry.Instrumentation.EntityFrameworkCore` exists only as a prerelease package. Npgsql, the PostgreSQL driver the services already use, emits its own spans, so the setup subscribes to them with `AddSource("Npgsql")` instead. The result is the same: one span per database call.

### 7. Addresses are configuration, not code

The Observability library reads the Seq and Zipkin addresses from configuration, with defaults matching the names in `docker-compose.yml`. The addresses are set per service in `docker-compose.yml`, so they can be changed without rebuilding.

### 8. Seq runs without authentication

Newer versions of Seq refuse to start without an admin password, so the container sets `SEQ_FIRSTRUN_NOAUTHENTICATION`. This is a development setup on a private Docker network; a real deployment would set a password instead.

## Known gaps (Week 4)

- Only DraftService sends log entries and traces; the other services still have to adopt the Observability library, and the diagrams show only what is built
- Metrics are not collected, only logs and traces; the dashboard mock-up for this week's other assignment relies mostly on metrics
- Seq and Zipkin store their data inside the containers, so `docker compose down -v` discards it
- Nothing alerts anyone; the Operator has to look

---

# Week 5 — Distributed tracing across service boundaries

The assignment: implement the remaining services around publishing, with the primary focus on distributed tracing. Traces must not break when they cross a service boundary, and all traces must be collected and stored centrally.

## What was built

| Container | Role |
| --- | --- |
| Webapp | Razor Pages front end where the Publisher writes an article and presses Publish |
| PublisherService | Takes the finished article and places it on the queue |
| ArticleQueue | RabbitMQ fanout exchange with one queue per subscriber |
| ArticleService | Extended: now also listens on the queue and stores what arrives |
| NewsletterService | Listens on the queue, and asks ArticleService for the articles of a newsletter |

The three services already built — ArticleService, CommentService and ProfanityService — were also put on the Observability library from Week 4, so every container that runs our own code now sends log entries to Seq and traces to Zipkin.

## How to run

From `src/HappyHeadlines`:

```
docker compose up --build articleservice publisherservice newsletterservice webapp loadbalancer
```

| Address | What it is |
| --- | --- |
| <http://localhost:8087> | Webapp: write and publish an article |
| <http://localhost:8086/newsletter/europe> | The newsletter for one continent |
| <http://localhost:15672> | RabbitMQ, user `guest`, password `guest` |
| <http://localhost:9411> | Zipkin |
| <http://localhost:5341> | Seq |

## The trace, end to end

Publishing an article from the browser produces one trace of seven spans across five services:

| Span | Service |
| --- | --- |
| post /index | Webapp |
| post articles | Webapp, calling out |
| post articles | PublisherService, receiving |
| publish articles | PublisherService, onto the exchange |
| receive article-queue-storage | ArticleService |
| db-europe: postgresql | ArticleService, writing |
| receive article-queue-newsletter | NewsletterService |

The two receives are siblings: one message, two subscribers, one trace.

## Design decisions

### 1. The trace id travels in the message headers

Across HTTP, OpenTelemetry passes the trace id in a request header on its own, and that is why the Week 4 services needed no code for it. A queue has no request to hang it on: the sender puts a message down and walks away, and the receiver picks it up later in another process. Without help, the receiver starts a new trace, and the chain breaks at exactly the point where it is hardest to follow.

`Observability/MessageTracing.cs` therefore does the same job by hand. `Inject` writes the trace id into the message headers before sending; `Extract` reads it back out on arrival, and the receiver passes it as the parent of its own span. Both use OpenTelemetry's own propagator, the same one used for HTTP, so the format is the standard `traceparent` and not something we invented. RabbitMQ stores headers as bytes, so the two ends convert to and from UTF-8.

The code lives in the shared library rather than in each service, for the reason given in Week 4, decision 3: it is infrastructure, not domain.

### 2. Sending and receiving each get their own span

The sender's span is `ActivityKind.Producer`, the receiver's `Consumer`. Without them, the queue would be an unexplained gap in the timeline. With them, the time a message spends waiting is visible, which is usually the interesting part.

Spans are only exported if OpenTelemetry is told which source they come from, so `ObservabilitySetup` subscribes to `MessageTracing.ActivitySourceName`.

### 3. A fanout exchange, not a shared queue

Two services need every article: one to store it, one for the newsletter. Sharing a single queue would not do that — RabbitMQ hands each message to one consumer, so the two would have split the articles between them and half would never have been stored.

The sender therefore publishes to a fanout exchange, which copies each message to every queue bound to it, and each service declares and binds its own queue (`article-queue-storage`, `article-queue-newsletter`). The sender knows nothing about who subscribes, which is the point of the queue in the first place.

### 4. Publishing answers 202, not 201

The article is not stored when PublisherService answers; it is on its way. `202 Accepted` says exactly that, where `201 Created` would claim something that has not happened yet. The delay is real, and it is what makes tracing through a queue worth doing.

### 5. Messages are acknowledged only after the work is done

Consumers run with `autoAck: false` and acknowledge after the article is stored. A failure sends the message back to the queue instead of dropping it. This means an article can be delivered twice if the service dies between storing and acknowledging — the usual trade in messaging, and the safer direction.

### 6. The Webapp is a real page, not just an endpoint

A form in the browser makes the trace start where a publisher actually starts, rather than at a command line. Razor Pages was chosen over a separate front-end project because it is the same project type, build and Dockerfile as the services, and because tracing a browser application would be an assignment of its own.

### 7. NewsletterService calls ArticleService through the load balancer

ArticleService runs as three instances behind Nginx (Week 2). Calling `http://loadbalancer:8080` keeps that split intact; calling one instance directly would quietly defeat it.

### 8. Monitoring moved to its own container diagram

Seven containers now send log entries and traces, which is fourteen arrows. On the container diagram they buried the system itself, so `Level2_Monitoring` shows them on their own and `Level2_Containers` excludes Seq, Zipkin and the Operator. Both diagrams come from the same model; only the views differ.

## Known gaps (Week 5)

- The deployment diagram still shows only Week 2 and Week 3: the new services, RabbitMQ, Seq and Zipkin are not on it
- Website and SubscriberService are still unbuilt, so the newsletter is never actually sent to anyone; `SubscriberQueue` remains unimplemented
- NewsletterService only logs the articles it receives from the queue, since it has no database to keep them in
- RabbitMQ runs with the default `guest` user and password, like the database passwords from Week 2
- Consumers are not idempotent: a redelivered message would store the article a second time

---

# Week 6 — Cache layers

The assignment: the global ArticleDatabase sits in North America and is not replicated, so European readers wait for every article. The Architectural Review Board rejected an X-axis split of the database on cost and approved cache layers instead. Two were to be built — one between ArticleService and the global ArticleDatabase, filled by an offline process with the articles of the last 14 days, and one between CommentService and CommentDatabase, filled on a cache miss, holding all comments for the 30 most recently accessed articles and cleaned up with the LRU algorithm — together with a small dashboard showing the hit ratio of each.

## What was built

| Part | File | Role |
| --- | --- | --- |
| ArticleCache | `ArticleService/Caching/ArticleCache.cs` | Holds the global articles of the last 14 days, keyed by article id. The whole contents are replaced at once, never one entry at a time. |
| ArticleCacheRefresher | `ArticleService/Caching/ArticleCacheRefresher.cs` | The offline process. A `BackgroundService` that reads the last 14 days of global articles every 30 seconds and hands them to the cache. |
| CommentCache | `CommentService/Caching/CommentCache.cs` | Holds all comments for the 30 most recently requested articles, keyed by continent and article id, with LRU eviction. |
| CacheMetrics | `Observability/CacheMetrics.cs` | Counts hits, misses and entries for both caches and exposes them to Prometheus. |
| Prometheus | `prometheus/prometheus.yml` | Collects the numbers from every service instance every five seconds. |
| Grafana | `grafana/provisioning/`, `grafana/dashboards/cache.json` | The dashboard, loaded from the repository rather than kept inside the container. |

The two cache layers are drawn as components in `docs/workspace.dsl`, in the views `Level3_ArticleCache` and `Level3_CommentCache`. They are the first level 3 diagrams in the model, because a cache that lives inside a service cannot be shown at the container level.

| Level | Diagram | Key / legend |
| --- | --- | --- |
| 3 — ArticleService | `docs/images/Level3_ArticleCache.png` | `docs/images/Level3_ArticleCache-key.png` |
| 3 — CommentService | `docs/images/Level3_CommentCache.png` | `docs/images/Level3_CommentCache-key.png` |

## How to run

From `src/HappyHeadlines`:

```
docker compose up --build
```

| Address | What it is |
| --- | --- |
| <http://localhost:8080> | Articles, through the load balancer |
| <http://localhost:8083> | Comments |
| <http://localhost:9090> | Prometheus: `Status > Targets` shows which instances are being collected from |
| <http://localhost:3000> | Grafana: the dashboard *Cache* |

The settings that the assignment states as numbers are in `docker-compose.yml`, not in the code: `ArticleCache__Days: "14"`, `ArticleCache__RefreshSeconds: "30"` and `CommentCache__Articles: "30"`.

The hit ratio is this query, once per cache:

```
sum(happyheadlines_cache_lookups_total{result="hit"}) by (cache)
/
sum(happyheadlines_cache_lookups_total) by (cache)
```

## Design decisions

### 1. The caches are written by hand, not Redis or Memcached

The assignment states both limits as counts: the articles of the last 14 days, and the 30 most recently accessed articles. Neither Redis nor Memcached can express that. They evict when they run out of memory, so the number of entries follows from how large the entries happen to be, and "exactly 30 articles" would have had to be enforced in our own code anyway.

The second reason is where the cache sits. ArticleService runs as three instances, and an in-process cache gives each instance a copy in its own region — which is the entire point of the story the assignment tells, where the problem is the distance from Europe to a database in North America. A shared Redis would sit in one place and be one more network hop away from two of the three instances.

The price is paid under "Known gaps": the three instances have three separate caches, and each one is empty after a restart.

### 2. Two different ways of filling a cache, deliberately

The article cache is filled by an offline process, so a reader never pays for the first request: the articles are already there when the request arrives. It costs a fixed amount of database work every 30 seconds whether anyone reads or not, which is affordable because the data set is small and bounded by 14 days.

The comment cache is filled by the request itself. The first reader of an article pays the full price and everyone after that is served from memory. It costs nothing when nobody reads, which matters because comments are not bounded: all comments for all articles would be far too much to keep.

The dashboard shows the difference as two numbers. The article cache sits at 100 per cent, because the cache was filled before anyone asked. The comment cache cannot reach 100 per cent by design: the first request for every article is a miss.

### 3. The article cache covers the global database only, and only lookups by id

The assignment says the cache goes between ArticleService and the global ArticleDatabase, so a request for an article in Europe or Asia goes straight to that continent's database as before. The list endpoint is also left alone, because it must keep showing every article; the cache only holds the last 14 days, and answering a request for all articles from it would silently drop the older ones.

### 4. Writing or deleting a comment empties that article's entry

`CommentsController` calls `Remove` after `SaveChangesAsync` on both `Post` and `Delete`. Without it, a reader who posted a comment would not see their own comment, because the cache would keep answering with the list from before the write.

The article cache needs nothing similar: the offline process replaces the whole contents on its next run.

### 5. LRU as a linked list plus a dictionary

The dictionary finds an entry by key without searching, and the linked list keeps the order of use, with the most recently used at the front. A lookup moves its node to the front, and an insertion that overflows removes the node at the back. Both are single operations that do not depend on how many entries the cache holds, which is the reason for keeping two structures instead of, for instance, sorting a list by a timestamp on every lookup.

### 6. prometheus-net, not the OpenTelemetry exporter

OpenTelemetry's Prometheus exporter for ASP.NET Core is still a prerelease package. `prometheus-net.AspNetCore` is a release, and the choice follows Week 4, decision 6, where the same reasoning kept the project on release packages. `MapMetrics()` in `ObservabilitySetup` opens `/metrics` in every service that uses the shared library, so both caches were reachable from Prometheus the moment they existed.

The counters are static fields, so every instance of `CacheMetrics` in a process writes into the same registry — which is what makes one endpoint able to report both caches, or several caches in one service.

### 7. Prometheus finds the ArticleService instances through DNS

ArticleService runs as three containers behind Nginx, and Prometheus collects from each service directly rather than through the load balancer: a load-balanced request would have returned one instance's numbers at random. `dns_sd_configs` asks Docker's own DNS for the addresses behind the name `articleservice` and collects from every one it gets back, so the configuration does not have to be changed when the number of instances changes. CommentService runs as a single container and is listed as a static target.

This is also the reason the dashboard adds up the instances with `sum(...) by (cache)`: without it, every panel would show one line per container.

### 8. The dashboard lives in the repository, not in Grafana

Grafana keeps dashboards in a database inside its own container, and the container has no named volume, so a `docker compose down` would take the dashboard with it. `grafana/dashboards/cache.json` is the dashboard as a file, and `grafana/provisioning/dashboards/dashboards.yml` tells Grafana to read it at start-up — the same arrangement as the Prometheus data source, which has been provisioned from a file since it was set up.

`allowUiUpdates: true` keeps the dashboard editable in the browser. Without it Grafana locks a provisioned dashboard, which is what happens to the data source: it has no "Save & test" button, only "Test".

## Known gaps (Week 6)

- The caches live in memory inside each service instance. They are empty after a restart, and the three ArticleService instances hold three separate copies, so `happyheadlines_cache_entries` is the sum across instances rather than the size of one cache
- A new global article is only in the cache after the next run of the offline process, so it can be up to 30 seconds old before it can be served from cache
- The hit ratio gauge counts every lookup since the service started, so a period of poor hit rate keeps weighing on the number long after it is over. The second panel, which measures over the last five minutes, is there for that reason
- Nothing limits the memory the caches use. The limits are 14 days and 30 articles, and a very long list of comments is still one entry
- Prometheus and Grafana are open without authentication, and Grafana's login form is disabled, in the same spirit as the database passwords from Week 2
- There are no tests of the LRU behaviour; the eviction was verified by hand, by requesting 31 articles and reading the log line that names the entry that was removed
