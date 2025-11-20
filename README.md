A production-grade, event-driven “Real-Time Log Ingestor” built with .NET 8, RabbitMQ, Postgres, Ocelot, and Redis. This README covers architecture, setup, run, testing, and operations, including DLQ handling and a stats API.

Overview
Ingestor API: Minimal API exposing POST /log to validate JSON and enqueue to RabbitMQ with durable, confirmed publishes, fronted by an API Gateway.​

Processor: .NET Worker consuming the queue, deserializing, and inserting into Postgres via Dapper with at-least-once guarantees and a DLQ path.​

DLQ Processor: Worker that drains logs.dlq, classifies errors, retries transient failures via a TTL retry queue, and parks irrecoverables.​

Gateway: Ocelot routing /log to the Ingestor; enables rate limiting and edge concerns.​

Dashboard API: GET /logs/stats querying Postgres for logs per minute; Redis caches results to offload the DB.
