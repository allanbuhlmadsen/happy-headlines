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
        ├── ArticleService/    ASP.NET Core Web API
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

A third diagram, added in Week 2, is described under "Deployment" below.

All three are generated from a single model in `docs/workspace.dsl`, written in
Structurizr DSL (a text format for describing C4 models). The key files explain
what the shapes and colours mean.

## How to view or regenerate

Online, without installing anything:

1. Open <https://structurizr.com/dsl>
2. Clear the editor and paste the contents of `docs/workspace.dsl`
3. Generate, then switch between the two views

Locally with Docker:

```
docker run -it --rm -p 8080:8080 -v /path/to/docs:/usr/local/structurizr structurizr/structurizr local
```

Then open <http://localhost:8080>. Note that this uses the same port as the
Week 2 load balancer, so do not run both at once.

## Model overview

Two people use the system: the **Publisher**, who drafts and publishes articles,
and the **Reader**, who reads articles, comments, and subscribes to the newsletter.

The system contains 16 containers:

- **Front ends (2)** — Webapp, Website
- **Services (7)** — DraftService, PublisherService, ProfanityService,
  ArticleService, CommentService, SubscriberService, NewsletterService
- **Queues (2)** — ArticleQueue, SubscriberQueue
- **Databases (5)** — DraftDatabase, ArticleDatabase, CommentDatabase,
  ProfanityDatabase, SubscriberDatabase

One external system: an **Email System** that delivers the newsletter.

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

Derivation is therefore switched off (`!impliedRelationships false`), and all three
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

## Known gaps

- The ArticleQueue subscription from the Week 1 model is not implemented; this
  week's requirements do not mention it
- The database password is committed in plain text. This is acceptable for a local
  development database with no real data, but is not a pattern to carry forward
