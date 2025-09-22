using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ESPNScrape.Models.Supabase;

[Table("SyncReports")]
public class SyncReportRecord : BaseModel
{
    [PrimaryKey("id")]
    public long id { get; set; }

    [Column("sync_id")]
    public string? sync_id { get; set; }

    [Column("sync_type")]
    public string? sync_type { get; set; }

    [Column("status")]
    public string? status { get; set; }

    [Column("start_time")]
    public DateTime start_time { get; set; }

    [Column("end_time")]
    public DateTime? end_time { get; set; }

    [Column("players_processed")]
    public int players_processed { get; set; }

    [Column("new_players_added")]
    public int new_players_added { get; set; }

    [Column("players_updated")]
    public int players_updated { get; set; }

    [Column("stats_records_processed")]
    public int stats_records_processed { get; set; }

    [Column("data_errors")]
    public int data_errors { get; set; }

    [Column("matching_errors")]
    public int matching_errors { get; set; }

    [Column("errors")]
    public string? errors { get; set; } // JSON array

    [Column("warnings")]
    public string? warnings { get; set; } // JSON array

    [Column("options")]
    public string? options { get; set; } // JSON object

    [Column("created_at")]
    public DateTime created_at { get; set; }
}

[Table("SyncMetrics")]
public class SyncMetricsRecord : BaseModel
{
    [PrimaryKey("id")]
    public long id { get; set; }

    [Column("date")]
    public DateTime date { get; set; }

    [Column("total_syncs")]
    public int total_syncs { get; set; }

    [Column("successful_syncs")]
    public int successful_syncs { get; set; }

    [Column("failed_syncs")]
    public int failed_syncs { get; set; }

    [Column("avg_duration_ms")]
    public double avg_duration_ms { get; set; }

    [Column("total_players_processed")]
    public int total_players_processed { get; set; }

    [Column("total_stats_processed")]
    public int total_stats_processed { get; set; }

    [Column("total_errors")]
    public int total_errors { get; set; }

    [Column("created_at")]
    public DateTime created_at { get; set; }

    [Column("updated_at")]
    public DateTime updated_at { get; set; }
}