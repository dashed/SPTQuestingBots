using Newtonsoft.Json;

namespace SPTQuestingBots.Telemetry;

public class TelemetryConfig
{
    [JsonProperty("enabled")]
    public bool Enabled { get; set; } = false;

    [JsonProperty("db_path")]
    public string DbPath { get; set; } = "log/telemetry.db";

    [JsonProperty("task_score_sample_interval_sec")]
    public float TaskScoreSampleIntervalSec { get; set; } = 2.0f;

    [JsonProperty("movement_sample_interval_sec")]
    public float MovementSampleIntervalSec { get; set; } = 1.0f;

    [JsonProperty("performance_sample_interval_sec")]
    public float PerformanceSampleIntervalSec { get; set; } = 5.0f;

    [JsonProperty("max_queue_depth")]
    public int MaxQueueDepth { get; set; } = 10000;

    [JsonProperty("batch_size")]
    public int BatchSize { get; set; } = 500;

    [JsonProperty("flush_interval_ms")]
    public int FlushIntervalMs { get; set; } = 1000;

    [JsonProperty("retention_raids")]
    public int RetentionRaids { get; set; } = 20;

    [JsonProperty("record_task_scores")]
    public bool RecordTaskScores { get; set; } = true;

    [JsonProperty("record_movement")]
    public bool RecordMovement { get; set; } = true;

    [JsonProperty("record_combat")]
    public bool RecordCombat { get; set; } = true;

    [JsonProperty("record_squad")]
    public bool RecordSquad { get; set; } = true;

    [JsonProperty("record_performance")]
    public bool RecordPerformance { get; set; } = true;

    [JsonProperty("record_errors")]
    public bool RecordErrors { get; set; } = true;
}
