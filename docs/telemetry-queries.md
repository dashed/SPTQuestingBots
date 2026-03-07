# Telemetry Query Cookbook

A collection of useful SQL queries for analyzing QuestingBots telemetry data. All queries run against the SQLite database at `BepInEx/plugins/DanW-SPTQuestingBots/log/telemetry.db`.

**Tools:** Use `sqlite3` CLI, [DB Browser for SQLite](https://sqlitebrowser.org/), or any SQLite-compatible tool.

---

## Raid Overview

### List all recorded raids

```sql
SELECT raid_id, map_id, start_time, end_time, bot_count, player_side, is_scav_raid
FROM raids
ORDER BY start_time DESC;
```

### Raid summary (bot count, kills, stucks, errors per raid)

```sql
SELECT
    r.raid_id,
    r.map_id,
    r.bot_count,
    (SELECT COUNT(*) FROM bot_events be WHERE be.raid_id = r.raid_id AND be.event_type = 'spawn') AS spawns,
    (SELECT COUNT(*) FROM bot_events be WHERE be.raid_id = r.raid_id AND be.event_type = 'death') AS deaths,
    (SELECT COUNT(*) FROM bot_events be WHERE be.raid_id = r.raid_id AND be.event_type = 'stuck') AS stucks,
    (SELECT COUNT(*) FROM combat_events ce WHERE ce.raid_id = r.raid_id AND ce.event_type = 'kill') AS kills,
    (SELECT COUNT(*) FROM errors e WHERE e.raid_id = r.raid_id) AS errors
FROM raids r
ORDER BY r.start_time DESC;
```

Expected output:
```
raid_id          | map_id    | bot_count | spawns | deaths | stucks | kills | errors
a1b2c3d4-...     | customs   | 18        | 18     | 12     | 2      | 8     | 0
e5f6g7h8-...     | factory4  | 8         | 8      | 6      | 0      | 5     | 1
```

---

## Task Analysis

### Task win frequency (which task is selected most)

```sql
SELECT
    active_task_name,
    COUNT(*) AS sample_count,
    ROUND(100.0 * COUNT(*) / (SELECT COUNT(*) FROM task_scores WHERE raid_id = ts.raid_id), 1) AS pct
FROM task_scores ts
WHERE raid_id = (SELECT raid_id FROM raids ORDER BY start_time DESC LIMIT 1)
GROUP BY active_task_name
ORDER BY sample_count DESC;
```

Expected output:
```
active_task_name | sample_count | pct
GoToObjective    | 342          | 38.2
Patrol           | 156          | 17.4
Loot             | 98           | 10.9
Ambush           | 87           | 9.7
...
```

### Task selection over time (per-minute breakdown)

```sql
SELECT
    CAST(raid_time / 60 AS INTEGER) AS minute,
    active_task_name,
    COUNT(*) AS count
FROM task_scores
WHERE raid_id = (SELECT raid_id FROM raids ORDER BY start_time DESC LIMIT 1)
GROUP BY minute, active_task_name
ORDER BY minute, count DESC;
```

### Task scores for a specific bot

```sql
SELECT raid_time, active_task_name, scores, personality, aggression
FROM task_scores
WHERE raid_id = (SELECT raid_id FROM raids ORDER BY start_time DESC LIMIT 1)
  AND bot_id = 1
ORDER BY raid_time;
```

---

## Combat Analysis

### Time to first kill per map

```sql
SELECT
    r.map_id,
    ROUND(MIN(ce.raid_time), 1) AS first_kill_sec,
    COUNT(DISTINCT r.raid_id) AS raids
FROM combat_events ce
JOIN raids r ON r.raid_id = ce.raid_id
WHERE ce.event_type = 'kill'
GROUP BY r.map_id
ORDER BY first_kill_sec;
```

Expected output:
```
map_id        | first_kill_sec | raids
factory4_day  | 12.3           | 5
customs       | 45.7           | 8
shoreline     | 78.2           | 3
```

### Kill leaderboard (top bots by kill count)

```sql
SELECT
    ce.bot_id,
    be.bot_name,
    be.bot_role,
    COUNT(*) AS kills
FROM combat_events ce
LEFT JOIN bot_events be ON be.raid_id = ce.raid_id AND be.bot_id = ce.bot_id AND be.event_type = 'spawn'
WHERE ce.event_type = 'kill'
  AND ce.raid_id = (SELECT raid_id FROM raids ORDER BY start_time DESC LIMIT 1)
GROUP BY ce.bot_id
ORDER BY kills DESC
LIMIT 10;
```

### Weapon usage distribution

```sql
SELECT weapon, COUNT(*) AS uses, ROUND(AVG(distance), 1) AS avg_distance
FROM combat_events
WHERE event_type = 'kill'
  AND raid_id = (SELECT raid_id FROM raids ORDER BY start_time DESC LIMIT 1)
GROUP BY weapon
ORDER BY uses DESC;
```

---

## Bot Lifecycle

### Bot lifespan analysis (spawn to death time)

```sql
SELECT
    spawn.bot_id,
    spawn.bot_name,
    spawn.bot_role,
    spawn.raid_time AS spawn_time,
    death.raid_time AS death_time,
    ROUND(death.raid_time - spawn.raid_time, 1) AS lifespan_sec
FROM bot_events spawn
LEFT JOIN bot_events death
    ON death.raid_id = spawn.raid_id
    AND death.bot_id = spawn.bot_id
    AND death.event_type = 'death'
WHERE spawn.event_type = 'spawn'
  AND spawn.raid_id = (SELECT raid_id FROM raids ORDER BY start_time DESC LIMIT 1)
ORDER BY lifespan_sec DESC;
```

Expected output:
```
bot_id | bot_name   | bot_role | spawn_time | death_time | lifespan_sec
3      | Reshala    | boss     | 5.2        | 892.1      | 886.9
7      | PMC_Bear_1 | pmc      | 12.0       | 445.3      | 433.3
1      | Scav_01    | scav     | 30.5       | 62.8       | 32.3
```

### Average lifespan by bot role

```sql
SELECT
    spawn.bot_role,
    COUNT(*) AS bot_count,
    ROUND(AVG(COALESCE(death.raid_time, 1200) - spawn.raid_time), 1) AS avg_lifespan_sec
FROM bot_events spawn
LEFT JOIN bot_events death
    ON death.raid_id = spawn.raid_id
    AND death.bot_id = spawn.bot_id
    AND death.event_type = 'death'
WHERE spawn.event_type = 'spawn'
  AND spawn.raid_id = (SELECT raid_id FROM raids ORDER BY start_time DESC LIMIT 1)
GROUP BY spawn.bot_role
ORDER BY avg_lifespan_sec DESC;
```

---

## Stuck Detection

### Stuck frequency by map

```sql
SELECT
    r.map_id,
    COUNT(*) AS stuck_count,
    COUNT(DISTINCT be.bot_id) AS unique_bots_stuck
FROM bot_events be
JOIN raids r ON r.raid_id = be.raid_id
WHERE be.event_type = 'stuck'
GROUP BY r.map_id
ORDER BY stuck_count DESC;
```

Expected output:
```
map_id      | stuck_count | unique_bots_stuck
reserve     | 12          | 8
customs     | 7           | 5
interchange | 3           | 3
```

### Stuck locations (find problem spots)

```sql
SELECT
    ROUND(pos_x, 0) AS x,
    ROUND(pos_y, 0) AS y,
    ROUND(pos_z, 0) AS z,
    COUNT(*) AS stuck_count
FROM bot_events
WHERE event_type = 'stuck'
  AND raid_id = (SELECT raid_id FROM raids ORDER BY start_time DESC LIMIT 1)
GROUP BY ROUND(pos_x, 0), ROUND(pos_y, 0), ROUND(pos_z, 0)
ORDER BY stuck_count DESC
LIMIT 20;
```

---

## Performance Analysis

### Performance over time (per-minute averages)

```sql
SELECT
    CAST(raid_time / 60 AS INTEGER) AS minute,
    ROUND(AVG(alive_bot_count), 0) AS avg_bots,
    ROUND(AVG(update_duration_ms), 2) AS avg_update_ms,
    ROUND(AVG(frame_time_ms), 2) AS avg_frame_ms,
    ROUND(AVG(memory_mb), 0) AS avg_memory_mb,
    ROUND(AVG(queue_depth), 0) AS avg_queue
FROM performance_samples
WHERE raid_id = (SELECT raid_id FROM raids ORDER BY start_time DESC LIMIT 1)
GROUP BY minute
ORDER BY minute;
```

Expected output:
```
minute | avg_bots | avg_update_ms | avg_frame_ms | avg_memory_mb | avg_queue
0      | 5        | 1.20          | 12.5         | 2048          | 0
1      | 12       | 2.45          | 14.2         | 2100          | 5
2      | 15       | 3.10          | 16.8         | 2150          | 12
```

### Peak frame time detection

```sql
SELECT raid_time, frame_time_ms, alive_bot_count, update_duration_ms
FROM performance_samples
WHERE raid_id = (SELECT raid_id FROM raids ORDER BY start_time DESC LIMIT 1)
ORDER BY frame_time_ms DESC
LIMIT 10;
```

---

## Squad Activity

### Squad event types and frequency

```sql
SELECT event_type, COUNT(*) AS count
FROM squad_events
WHERE raid_id = (SELECT raid_id FROM raids ORDER BY start_time DESC LIMIT 1)
GROUP BY event_type
ORDER BY count DESC;
```

### Formation changes over time

```sql
SELECT
    raid_time,
    squad_id,
    event_type,
    detail
FROM squad_events
WHERE raid_id = (SELECT raid_id FROM raids ORDER BY start_time DESC LIMIT 1)
  AND event_type IN ('formation_change', 'strategy_change')
ORDER BY raid_time;
```

---

## Movement Analysis

### Average bot speed by event type

```sql
SELECT
    event_type,
    COUNT(*) AS samples,
    ROUND(AVG(speed), 2) AS avg_speed,
    ROUND(MAX(speed), 2) AS max_speed
FROM movement_events
WHERE raid_id = (SELECT raid_id FROM raids ORDER BY start_time DESC LIMIT 1)
GROUP BY event_type
ORDER BY avg_speed DESC;
```

### Movement heatmap data (position frequency grid)

```sql
SELECT
    ROUND(pos_x / 10, 0) * 10 AS grid_x,
    ROUND(pos_z / 10, 0) * 10 AS grid_z,
    COUNT(*) AS visits
FROM movement_events
WHERE raid_id = (SELECT raid_id FROM raids ORDER BY start_time DESC LIMIT 1)
GROUP BY grid_x, grid_z
ORDER BY visits DESC
LIMIT 50;
```

---

## Error Analysis

### Error frequency and top error messages

```sql
SELECT
    source,
    severity,
    message,
    COUNT(*) AS occurrences
FROM errors
WHERE raid_id = (SELECT raid_id FROM raids ORDER BY start_time DESC LIMIT 1)
GROUP BY source, severity, message
ORDER BY occurrences DESC
LIMIT 20;
```

### Errors over time

```sql
SELECT
    CAST(raid_time / 60 AS INTEGER) AS minute,
    severity,
    COUNT(*) AS error_count
FROM errors
WHERE raid_id = (SELECT raid_id FROM raids ORDER BY start_time DESC LIMIT 1)
GROUP BY minute, severity
ORDER BY minute;
```

---

## Cross-Raid Analysis

### Map difficulty ranking (deaths per bot)

```sql
SELECT
    r.map_id,
    COUNT(DISTINCT r.raid_id) AS raids,
    SUM(r.bot_count) AS total_bots,
    COUNT(CASE WHEN be.event_type = 'death' THEN 1 END) AS total_deaths,
    ROUND(1.0 * COUNT(CASE WHEN be.event_type = 'death' THEN 1 END) / NULLIF(SUM(r.bot_count), 0), 2) AS deaths_per_bot
FROM raids r
LEFT JOIN bot_events be ON be.raid_id = r.raid_id
GROUP BY r.map_id
ORDER BY deaths_per_bot DESC;
```

### Performance trend across raids

```sql
SELECT
    r.raid_id,
    r.map_id,
    r.start_time,
    r.bot_count,
    ROUND(AVG(ps.frame_time_ms), 2) AS avg_frame_ms,
    ROUND(MAX(ps.frame_time_ms), 2) AS peak_frame_ms
FROM raids r
JOIN performance_samples ps ON ps.raid_id = r.raid_id
GROUP BY r.raid_id
ORDER BY r.start_time DESC;
```
