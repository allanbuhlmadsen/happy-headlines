workspace "Happy Headlines" "C4 model: context, containers, and deployment" {
    !impliedRelationships false
    model {
        # ---------- People ----------
        publisher = person "Publisher" "Journalist or editor who drafts and publishes articles."
        reader = person "Reader" "Reads articles, posts comments, and subscribes to the newsletter."
        operator = person "Operator" "Developer on call who watches the monitoring dashboard and handles incidents."

        # ---------- External systems ----------
        emailSystem = softwareSystem "Email System" "Delivers the newsletter to subscribers' inboxes." "External"
        monitoringSystem = softwareSystem "Monitoring System" "Bought, not built (Grafana stack). Stores metrics, logs, and traces, shows the dashboard, and sends alerts." "External"

        # ---------- Happy Headlines ----------
        happyHeadlines = softwareSystem "Happy Headlines" "Positive news platform: drafting, publishing, reading, commenting, and the newsletter." {

            # --- Front ends ---
            webapp = container "Webapp" "Editorial front end where the Publisher drafts and publishes articles."
            website = container "Website" "Public front end where the Reader reads articles, comments, and subscribes."

            # --- Services ---
            draftService = container "DraftService" "Stores and retrieves drafts."
            publisherService = container "PublisherService" "Finalises the publication of an article."
            profanityService = container "ProfanityService" "Filters inappropriate language in articles and comments."
            articleService = container "ArticleService" "Delivers articles to the Website and the NewsletterService."
            commentService = container "CommentService" "Receives, filters, and stores comments."
            subscriberService = container "SubscriberService" "Handles sign-ups and subscriber data."
            newsletterService = container "NewsletterService" "Assembles and sends the daily newsletter."

            # --- Monitoring ---
            telemetryCollector = container "TelemetryCollector" "Bought, not built (OpenTelemetry Collector). Collects metrics, logs, and traces from all services and forwards them." "OpenTelemetry Collector"

            # --- Queues ---
            articleQueue = container "ArticleQueue" "Carries approved articles from publishing to storage." "" "Queue"
            subscriberQueue = container "SubscriberQueue" "Carries new sign-ups." "" "Queue"

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
        newsletterService -> subscriberService "Fetches all active subscribers when sending"
        newsletterService -> emailSystem "Sends the newsletter"

        # ---------- Relationships: filtering ----------
        profanityService -> profanityDatabase "Retrieves and removes prohibited words"

        # ---------- Relationships: monitoring ----------
        webapp -> telemetryCollector "Sends metrics, logs, and traces to"
        website -> telemetryCollector "Sends metrics, logs, and traces to"
        draftService -> telemetryCollector "Sends metrics, logs, and traces to"
        publisherService -> telemetryCollector "Sends metrics, logs, and traces to"
        profanityService -> telemetryCollector "Sends metrics, logs, and traces to"
        articleService -> telemetryCollector "Sends metrics, logs, and traces to"
        commentService -> telemetryCollector "Sends metrics, logs, and traces to"
        subscriberService -> telemetryCollector "Sends metrics, logs, and traces to"
        newsletterService -> telemetryCollector "Sends metrics, logs, and traces to"
        telemetryCollector -> monitoringSystem "Forwards metrics, logs, and traces to"
        operator -> monitoringSystem "Watches the dashboard in"
        monitoringSystem -> operator "Alerts about incidents"

        # ---------- System-level relationships (level 1) ----------
        # Implied relationships are disabled, so these are stated explicitly.
        publisher -> happyHeadlines "Drafts and publishes articles"
        reader -> happyHeadlines "Reads articles, posts comments, and subscribes to the newsletter"
        happyHeadlines -> emailSystem "Sends the newsletter"
        happyHeadlines -> monitoringSystem "Sends metrics, logs, and traces to"

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
            include operator
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
        }

        theme default
    }
}
