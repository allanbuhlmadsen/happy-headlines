# Happy Headlines — Semester Project

Architecture work for the Happy Headlines semester project.

## Week 1 — C4 model, levels 1 and 2

This week's deliverable is a C4 model of the system described in the assignment,
covering the context level and the container level. No implementation.

### Diagrams

| Level | Diagram | Key / legend |
|---|---|---|
| 1 — System Context | `docs/images/Level1_Context.png` | `docs/images/Level1_Context-key.png` |
| 2 — Containers | `docs/images/Level2_Containers.png` | `docs/images/Level2_Containers-key.png` |

Both are generated from `docs/workspace.dsl`. The key files explain what the
shapes and colours mean.

Both diagrams are generated from a single model in `docs/workspace.dsl`,
written in Structurizr DSL (a text format for describing C4 models).

### How to view or regenerate

Online, without installing anything:

1. Open <https://structurizr.com/dsl>
2. Clear the editor and paste the contents of `docs/workspace.dsl`
3. Generate, then switch between the two views

Locally with Docker:

```
docker run -it --rm -p 8080:8080 -v /path/to/docs:/usr/local/structurizr structurizr/structurizr local
```

Then open <http://localhost:8080>.

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

## Notation

- Rectangles are containers; cylinders are databases; pipes are queues
- Grey elements sit outside the system boundary
- Arrows point from the initiator of an interaction, which is not always the
  direction data flows — the Website calls ArticleService, though articles come back
- Queues have no arrow passing through them; the producer and consumer do not
  know about each other, which is the point of using a queue
