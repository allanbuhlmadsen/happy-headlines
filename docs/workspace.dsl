workspace "Happy Headlines" "C4 model: context, containers, and deployment" {
    !impliedRelationships false
    model {
        # ---------- People ----------
        publisher = person "Publisher" "Journalist or editor who drafts and publishes articles."
        reader = person "Reader" "Reads articles, posts comments, and subscribes to the newsletter."
        operator = person "Operator" "Developer on call who reads the log entries and traces and handles incidents."

        # ---------- External systems ----------
        emailSystem = softwareSystem "Email System" "Delivers the newsletter to subscribers' inboxes." "External"
        seq = softwareSystem "Seq" "Bought, not built. Stores the structured log entries from the services and shows them in a browser." "External"
        zipkin = softwareSystem "Zipkin" "Bought, not built. Stores the traces from the services and shows each request as a timeline." "External"
        featureHub = softwareSystem "FeatureHub" "Bought, not built. Holds the release toggles and pushes a change out to the services while they run." "External"

        # ---------- Happy Headlines ----------
        happyHeadlines = softwareSystem "Happy Headlines" "Positive news platform: drafting, publishing, reading, commenting, and the newsletter." {

            # --- Front ends ---
            webapp = container "Webapp" "Editorial front end where the Publisher drafts and publishes articles." "Razor Pages"
            website = container "Website" "Public front end where the Reader reads articles, comments, and subscribes."

            # --- Services ---
            draftService = container "DraftService" "Stores and retrieves drafts." "REST API"
            publisherService = container "PublisherService" "Finalises the publication of an article." "REST API"
            profanityService = container "ProfanityService" "Filters inappropriate language in articles and comments." "REST API"
            articleService = container "ArticleService" "Delivers articles to the Website and the NewsletterService." "REST API" {
                articlesController = component "ArticlesController" "Takes the requests for articles and answers them. Five endpoints, all scoped to a continent." "ASP.NET Core controller"
                articleQueueListener = component "ArticleQueueListener" "Receives approved articles from the queue and stores them. Acknowledges only once the article is written." "BackgroundService"
                continentDbContextFactory = component "ContinentDbContextFactory" "Knows the eight continent names, validates them, and builds a database context for one of them. The coordination component of the Z-axis split." "C# class"
                articleCache = component "ArticleCache" "Holds the global articles from the last 14 days in memory. Read on every request for a global article." "C# Dictionary" "Cache"
                articleCacheRefresher = component "ArticleCacheRefresher" "Offline process. Every 30 seconds it reads the last 14 days of global articles and replaces the contents of the cache." "BackgroundService"
            }
            commentService = container "CommentService" "Receives, filters, and stores comments." "REST API" {
                commentsController = component "CommentsController" "Takes the requests for comments and answers them." "ASP.NET Core controller"
                commentCache = component "CommentCache" "Holds all comments for the 30 most recently requested articles. Filled on a cache miss and emptied with the LRU algorithm." "C# LinkedList and Dictionary" "Cache"
            }
            subscriberService = container "SubscriberService" "Handles sign-ups and subscriber data. The whole service sits behind a release toggle." "REST API"
            newsletterService = container "NewsletterService" "Assembles and sends the daily newsletter." "REST API"

            # --- Queues ---
            articleQueue = container "ArticleQueue" "Carries approved articles to everyone who subscribes: a copy for storage and a copy for the newsletter." "RabbitMQ fanout exchange" "Queue"
            subscriberQueue = container "SubscriberQueue" "Carries new sign-ups." "RabbitMQ fanout exchange" "Queue"

            # --- Databases ---
            draftDatabase = container "DraftDatabase" "Drafts." "" "Database"
            articleDatabase = container "ArticleDatabase" "Published articles." "" "Database"
            commentDatabase = container "CommentDatabase" "Comments." "" "Database"
            profanityDatabase = container "ProfanityDatabase" "Prohibited words." "" "Database"
            subscriberDatabase = container "SubscriberDatabase" "Subscribers." "" "Database"
        }

        # ---------- Relationships: publishing ----------
        publisher -> webapp "Drafts and publishes articles"
        webapp -> draftService "Saves and retrieves drafts"
        draftService -> draftDatabase "Reads from and writes drafts to"
        webapp -> publisherService "Submits a finished article for publication"
        publisherService -> profanityService "Requests filtering of the article text"
        publisherService -> articleQueue "Places the approved article on"
        articleQueue -> articleService "Delivers new articles to"
        articleQueue -> newsletterService "Delivers new articles to"
        articleService -> articleDatabase "Stores and retrieves articles"

        # ---------- Relationships: reading ----------
        reader -> website "Reads articles"
        website -> articleService "Fetches recent articles and the highlighted article"

        # ---------- Relationships: commenting ----------
        reader -> website "Posts comments"
        website -> commentService "Submits and retrieves comments"
        commentService -> profanityService "Requests filtering of the comment text"
        commentService -> commentDatabase "Stores and retrieves comments"

        # ---------- Relationships: subscribing ----------
        reader -> website "Subscribes to the newsletter"
        website -> subscriberService "Submits the sign-up"
        subscriberService -> subscriberDatabase "Stores and retrieves subscriber data"
        subscriberService -> subscriberQueue "Places the new sign-up on"
        subscriberQueue -> newsletterService "Delivers new sign-ups to"
        newsletterService -> articleService "Fetches recent articles"
        newsletterService -> subscriberService "Fetches all active subscribers when sending, behind a circuit breaker"
        newsletterService -> emailSystem "Sends the newsletter"

        # ---------- Relationships: caching (level 3) ----------
        # The two cache layers sit inside the services, between the controller
        # and the database. They are filled in two different ways.
        articlesController -> articleCache "Looks the article up in"
        articleCacheRefresher -> articleCache "Replaces the whole contents of"

        # Nothing inside ArticleService reaches a database on its own. Every
        # component asks the factory, which is what keeps the Z-axis split in
        # one place.
        articlesController -> continentDbContextFactory "Asks for a context for the continent in the route"
        articleQueueListener -> continentDbContextFactory "Asks for a context for the article's continent"
        articleCacheRefresher -> continentDbContextFactory "Asks for a context for the global database"
        continentDbContextFactory -> articleDatabase "Creates a context pointed at that continent's database"
        articleQueue -> articleQueueListener "Delivers new articles to"

        commentsController -> commentCache "Looks the article's comments up in, puts them into, and removes them from"
        commentsController -> commentDatabase "Reads the comments from, when they are not in the cache, and writes new comments to"

        # ---------- Relationships: filtering ----------
        profanityService -> profanityDatabase "Retrieves and removes prohibited words"

        # ---------- Relationships: monitoring ----------
        # Every container that runs our own code uses the Observability library.
        webapp -> seq "Sends log entries to"
        webapp -> zipkin "Sends traces to"
        draftService -> seq "Sends log entries to"
        draftService -> zipkin "Sends traces to"
        publisherService -> seq "Sends log entries to"
        publisherService -> zipkin "Sends traces to"
        articleService -> seq "Sends log entries to"
        articleService -> zipkin "Sends traces to"
        newsletterService -> seq "Sends log entries to"
        newsletterService -> zipkin "Sends traces to"
        commentService -> seq "Sends log entries to"
        commentService -> zipkin "Sends traces to"
        profanityService -> seq "Sends log entries to"
        profanityService -> zipkin "Sends traces to"
        subscriberService -> seq "Sends log entries to"
        subscriberService -> zipkin "Sends traces to"
        operator -> seq "Reads log entries in"
        operator -> zipkin "Reads traces in"

        # ---------- Relationships: release toggles ----------
        # Both services read the same flag, so the subscriber feature is turned
        # on and off as a whole, without either of them being redeployed.
        subscriberService -> featureHub "Reads the release toggle from"
        newsletterService -> featureHub "Reads the release toggle from"

        # ---------- System-level relationships (level 1) ----------
        # Implied relationships are disabled, so these are stated explicitly.
        publisher -> happyHeadlines "Drafts and publishes articles"
        reader -> happyHeadlines "Reads articles, posts comments, and subscribes to the newsletter"
        happyHeadlines -> emailSystem "Sends the newsletter"
        happyHeadlines -> seq "Sends log entries to"
        happyHeadlines -> zipkin "Sends traces to"
        happyHeadlines -> featureHub "Reads release toggles from"

        # ---------- Deployment ----------
        deploymentEnvironment "Docker Compose" {
            deploymentNode "Developer machine" "" "Docker Compose" {

                loadBalancer = infrastructureNode "loadbalancer" "Distributes requests across the service instances, round robin." "Nginx"

                deploymentNode "articleservice" "X-axis split: three identical instances." "Docker container" "" 3 {
                    serviceInstance = containerInstance articleService
                }

                deploymentNode "db-africa" "" "Docker container" {
                    containerInstance articleDatabase
                }
                deploymentNode "db-antarctica" "" "Docker container" {
                    containerInstance articleDatabase
                }
                deploymentNode "db-asia" "" "Docker container" {
                    containerInstance articleDatabase
                }
                deploymentNode "db-europe" "" "Docker container" {
                    containerInstance articleDatabase
                }
                deploymentNode "db-northamerica" "" "Docker container" {
                    containerInstance articleDatabase
                }
                deploymentNode "db-oceania" "" "Docker container" {
                    containerInstance articleDatabase
                }
                deploymentNode "db-southamerica" "" "Docker container" {
                    containerInstance articleDatabase
                }
                deploymentNode "db-global" "" "Docker container" {
                    containerInstance articleDatabase
                }
                deploymentNode "profanityservice" "Own swimlane. No load balancer in front." "Docker container" {
                    profanityInstance = containerInstance profanityService
                }

                deploymentNode "db-profanity" "" "Docker container" {
                    containerInstance profanityDatabase
                }

                deploymentNode "commentservice" "Own swimlane. Calls ProfanityService directly, behind a circuit breaker." "Docker container" {
                    commentInstance = containerInstance commentService
                }

                deploymentNode "db-comments" "" "Docker container" {
                    containerInstance commentDatabase
                }
            }

            loadBalancer -> serviceInstance "Forwards requests to"
        }
    }

    views {
        systemContext happyHeadlines "Level1_Context" "The system's users and its surroundings." {
            include *
            include operator
            autolayout lr
        }

        container happyHeadlines "Level2_Containers" "The system's containers and how they interact." {
            include *
            exclude seq zipkin operator
            autolayout lr
        }

        container happyHeadlines "Level2_Monitoring" "Which containers send log entries and traces, and where they go." {
            include webapp draftService publisherService articleService newsletterService commentService profanityService subscriberService seq zipkin operator
            autolayout lr
        }

        component articleService "Level3_ArticleService" "Inside ArticleService: the controller, the queue listener, the cache and its offline refresher, and the factory that picks a continent's database." {
            include *
            autolayout lr
        }

        component commentService "Level3_CommentCache" "The cache layer between CommentService and the CommentDatabase, filled on a cache miss." {
            include *
            autolayout lr
        }

        deployment happyHeadlines "Docker Compose" "Deployment" "How the services and their databases are actually run." {
            include *
            autolayout lr
        }

        styles {
            element "Person" {
                shape person
                background #08427b
                color #ffffff
            }
            element "Software System" {
                background #1168bd
                color #ffffff
            }
            element "External" {
                background #999999
                color #ffffff
            }
            element "Container" {
                background #438dd5
                color #ffffff
            }
            element "Database" {
                shape cylinder
                background #438dd5
                color #ffffff
            }
            element "Queue" {
                shape pipe
                background #438dd5
                color #ffffff
            }
            element "Component" {
                background #85bbf0
                color #000000
            }
            element "Cache" {
                shape hexagon
                background #f5a623
                color #000000
            }
        }

        theme default
    }
}
