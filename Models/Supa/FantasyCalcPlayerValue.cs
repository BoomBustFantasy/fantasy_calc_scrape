using System.Text.Json.Serialization;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace FantasyCalcScrape.Models.Supa;

[Table("FantasyCalcPlayerValues")]
public class FantasyCalcPlayerValue : BaseModel
{
    [PrimaryKey("id")]
    [Column("id")]
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [Column("player_id")]
    [JsonPropertyName("player_id")]
    public long PlayerId { get; set; }

    [Column("mode")]
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "DYN";

    [Column("num_teams")]
    [JsonPropertyName("num_teams")]
    public int NumTeams { get; set; }

    [Column("num_qbs")]
    [JsonPropertyName("num_qbs")]
    public int NumQbs { get; set; }

    [Column("ppr")]
    [JsonPropertyName("ppr")]
    public decimal Ppr { get; set; }

    [Column("te_premium")]
    [JsonPropertyName("te_premium")]
    public string TePremium { get; set; } = "NOTEP";

    [Column("fantasy_calc_player_id")]
    [JsonPropertyName("fantasy_calc_player_id")]
    public int FantasyCalcPlayerId { get; set; }

    [Column("value")]
    [JsonPropertyName("value")]
    public int Value { get; set; }

    [Column("overall_rank")]
    [JsonPropertyName("overall_rank")]
    public int OverallRank { get; set; }

    [Column("position_rank")]
    [JsonPropertyName("position_rank")]
    public int PositionRank { get; set; }

    [Column("updated_at")]
    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }
}