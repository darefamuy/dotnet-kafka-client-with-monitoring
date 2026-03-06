# Key Client Metrics
## Key Kafka Producer Metrics
| Metric | Meaning | Recommended Alert Thresholds |
| --- | --- | --- |
| `request-latency-avg` | Avg time (ms) for requests to get a response from broker | > 200ms |
| `record-error-rate` | Errors as a result of failed sends per second | > 0
| `record-retry-rate` | Retries per second | sustained > 0 |
| `buffer-exhausted-rate` | Rate at which buffer pool is full (leads to blocking) | > 0.1 |
| `produce-throttle-time` | Broker-side throttling time (ms) | consistently > 100ms |
| `io-wait-time-ns-avg` | Average time (in nanoseconds) I/O threads wait for socket readiness (i.e., nothing to read/write) | Warning: > 5 ms, Critical: > 20 ms (p95 over 5m) |
| `io-wait-ratio` | Proportion of time threads spend waiting (vs. doing actual work) | ⚠️ Warning: > 0.15 (15%), 🔥 Critical: > 0.5 (50%) |
| `io-time-ns-avg` | Avg time (in ns) Kafka threads are spending on actual I/O (read/write over the network) | ⚠️ Warning: > 10 ms, 🔥 Critical: > 40 ms (p95 over 5m) |
| `io-ratio` | Ratio of total thread time spent doing I/O | ⚠️ Warning: > 0.3 , 🔥 Critical: > 0.8 (avg over 5m)|


## Troubleshooting Producer Timeouts & Performance Issues
| Symptom | What to Look For | Possible Root Causes |
| --- | --- | --- |
| Produce requests timeout | High `io-wait-ratio`, low `select-rate` | Broker not responding or under load |
| Low throughput | Low `io-ratio`, high `buffer-exhausted-rate` | Producer blocked due to backpressure |
| High CPU usage | High `select-rate`, high `io-time-ns-avg` | Network thrashing, large number of partitions |
| Idle producer | High `io-wait-time-ns-avg`, low `record-send-rate` | No new data to send, or stuck in queue |
| Broker throttling | Low `connection-count`, high `produce-throttle-time-avg` | Kafka quotas or overloaded brokers |


## Key Kafka Consumer Metrics
| **Metric** | **Meaning** | **Recommended Alert Thresholds** |
| ----------- | -------------------------------- | ---------- |
| `records-lag-max` | Maximum number of messages the consumer lags behind the latest message in the partition. Indicates how far behind the consumer is. | ⚠️ Warning: > 1,000 messages for >5 min  , 🔥 Critical: > 10,000 messages for >10 min  |
| `records-consumed-rate`   | Rate of records consumed per second from Kafka. Indicates consumer throughput.   | ⚠️ Warning: Drop > 50%, 🔥 Critical: Drop > 80% (p95 over 5m) |
| `fetch-latency-avg` | Average latency of fetch requests from the broker. Indicates network or broker load issues. | ⚠️ Warning: > 50 ms , 🔥 > 200 ms  |
| `fetch-latency-max` | Maximum fetch latency observed. Indicates intermittent delays in data fetch.   | ⚠️ Warning: > 250 ms , 🔥 > 1 s |
| `fetch-rate`  | Number of fetch requests sent per second. Should correlate with consumption rate. | ⚠️ Warning: Sudden drop > 50% (p95 over 5m) |
| `bytes-consumed-rate`  | Rate of bytes consumed per second. Indicates consumer data throughput.   | ⚠️ Warning: Drop > 50% , 🔥 Drop > 80%  |
| `records-per-request-avg` | Average records fetched per request. Low values mean inefficient fetching.  | ⚠️ Warning: < 50 records/request , 🔥 < 10 records/request |
| `commit-latency-avg`   | Average latency for committing offsets. Affects message acknowledgment speed.  | ⚠️ Warning: > 100 ms , 🔥 Critical: > 500 ms |
| `commit-rate` | Rate of successful offset commits. Should be stable and non-zero when consuming.  | ⚠️ Warning: Drop > 50% , 🔥 Critical: Zero (p95 over 5m) |
| `rebalances-total`  | Total number of consumer group rebalances. High frequency indicates instability.  | ⚠️ Warning: > 3 per hour , 🔥 Critical: > 10 per hour |
| `heartbeat-rate` | Frequency of heartbeats sent to coordinator. Drop means possible disconnection.   | ⚠️ Warning: Drop > 50% , 🔥 Critical: Zero for >2× session.timeout.ms |
| `join-rate`   | Rate of joining consumer groups. Spikes mean frequent group rebalances.  | ⚠️ Warning: > 3 joins/minute , 🔥 Critical: Sustained > 5/minute  |
| `poll-latency-avg`  | Average time spent in poll() loop. High values indicate blocked processing. | ⚠️ Warning: > 200 ms , 🔥 Critical: > 1 s |
| `poll-rate`   | Number of poll() calls per second. Should remain consistent. | ⚠️ Warning: Drop > 50% , 🔥 Critical: Zero for > session.timeout.ms   |
| `assignment-lost-rate` | Rate of partition assignment losses. Indicates instability in group coordination. | ⚠️ Warning: > 0.01/sec sustained , 🔥 Critical: > 0.1/sec sustained  |

## Troubleshooting Consumer Failures, Timeouts & Performance Issues
| **Symptom**  | **What to Look For (Metrics / Logs)** | **Possible Root Causes**  |
| --------- | --------------- | --------------- |
| **High consumer lag (`records-lag-max`)**    | Check if `records-consumed-rate` or `fetch-rate` dropped; inspect broker load, network latency.  | Consumer slowed (CPU, GC, I/O bottlenecks), network latency, unbalanced partitions, broker overload, downstream system (e.g., DB) blocking processing. |
| **Frequent consumer rebalances (`rebalances-total`, `join-rate`)**     | Look for churn in consumer logs, heartbeat failures, or dropped connections.   | Consumer group instability, heartbeat timeout too low, long GC pauses, frequent restarts, uneven partition assignment.  |
| **Low consumption rate (`records-consumed-rate`, `bytes-consumed-rate`)**   | Compare to producer throughput; check poll rate and processing latency.   | Application slowdown, external dependency blocking (e.g., DB/API calls), GC or thread contention.     |
| **Offset commits delayed or failing (`commit-latency-avg`, `commit-rate`)** | Look for offset commit exceptions in logs; check `commit-rate` and consumer coordinator metrics. | Coordinator or broker under load, network timeouts, slow offset store (if external), misconfigured `enable.auto.commit`.  |
| **Consumer stuck or not progressing**   | `poll-rate` drops to zero, `heartbeat-rate` drops, high `poll-latency-avg`.    | Application logic blocking poll loop, long message processing, thread starvation, or deserialization errors. |
| **High fetch latency (`fetch-latency-avg` / `max`)** | Compare with broker network I/O; check client-side network stats. | Broker overloaded, slow disks, high broker network I/O, poor network connectivity between consumer and broker.     |
| **Frequent lost assignments (`assignment-lost-rate`)**    | Look for group rebalances and connection resets.  | Misconfigured `max.poll.interval.ms`, consumer too slow, coordinator instability.    |
| **Spikes in rebalances and joins (`rebalances-total`, `join-rate`)**   | Check logs for “Member Rejoined” or “Group Rebalance in Progress.”  | Consumers dying unexpectedly, scaling events, frequent partition changes, unstable group management.  |
| **Slow message processing**  | Look at `poll-latency-avg`, `records-per-request-avg`, `commit-latency-avg`.   | Heavy message transformation, synchronous I/O (e.g., DB writes), lack of async processing, thread starvation.      |
| **Heartbeat failures**     | Monitor `heartbeat-rate` and compare to `session.timeout.ms`.     | Consumer blocked in poll() or GC, misconfigured timeout, overloaded JVM or network delays.     |



