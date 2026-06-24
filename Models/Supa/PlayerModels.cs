using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace FantasyCalcScrape.Models.Supa;

/// <summary>
/// Represents a player in the Supabase Players table
/// </summary>
[Table("Players")]
public class Player : BaseModel
{
    /// <summary>
    /// Primary key - Auto-generated identity
    /// </summary>
    [PrimaryKey("id")]
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>
    /// Player's first name
    /// </summary>
    [Column("first_name")]
    [JsonPropertyName("first_name")]
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Player's last name
    /// </summary>
    [Column("last_name")]
    [JsonPropertyName("last_name")]
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to Teams table
    /// </summary>
    [Column("team_id")]
    [JsonPropertyName("team_id")]
    public long TeamId { get; set; }

    /// <summary>
    /// Foreign key to Positions table
    /// </summary>
    [Column("position_id")]
    [JsonPropertyName("position_id")]
    public long? PositionId { get; set; }

    /// <summary>
    /// Player's age
    /// </summary>
    [Column("age")]
    [JsonPropertyName("age")]
    public decimal? Age { get; set; }

    /// <summary>
    /// Whether the player is currently active
    /// </summary>
    [Column("active")]
    [JsonPropertyName("active")]
    public bool? Active { get; set; } = false;

    /// <summary>
    /// Player's positional rank
    /// </summary>
    [Column("positional_rank")]
    [JsonPropertyName("positional_rank")]
    public int? PositionalRank { get; set; }

    /// <summary>
    /// Player's overall rank
    /// </summary>
    [Column("overall_rank")]
    [JsonPropertyName("overall_rank")]
    public int? OverallRank { get; set; }

    // External Platform IDs
    /// <summary>
    /// ESPN API player identifier for data synchronization
    /// </summary>
    [Column("espn_player_id")]
    [JsonPropertyName("espn_player_id")]
    public string? EspnPlayerId { get; set; }

    /// <summary>
    /// Fantasy Sharks player ID
    /// </summary>
    [Column("fantasy_sharks_player_id")]
    [JsonPropertyName("fantasy_sharks_player_id")]
    public long? FantasySharksPlayerId { get; set; }

    /// <summary>
    /// Pro Football Reference player ID
    /// </summary>
    [Column("pro_football_reference_id")]
    [JsonPropertyName("pro_football_reference_id")]
    public long? ProFootballReferenceId { get; set; }

    /// <summary>
    /// Pro Football Reference player code
    /// </summary>
    [Column("pfr_player_code")]
    [JsonPropertyName("pfr_player_code")]
    public string? PfrPlayerCode { get; set; }

    /// <summary>
    /// Sleeper platform player ID
    /// </summary>
    [Column("sleeper_id")]
    [JsonPropertyName("sleeper_id")]
    public long? SleeperId { get; set; }

    /// <summary>
    /// KTC (KeepTradeCut) player ID
    /// </summary>
    [Column("ktc_player_id")]
    [JsonPropertyName("ktc_player_id")]
    public string? KtcPlayerId { get; set; }

    /// <summary>
    /// KTC (KeepTradeCut) player link
    /// </summary>
    [Column("ktc_player_link")]
    [JsonPropertyName("ktc_player_link")]
    public string? KtcPlayerLink { get; set; }

    /// <summary>
    /// KTC (KeepTradeCut) value for the player from dynasty rankings
    /// </summary>
    [Column("ktc_value")]
    [JsonPropertyName("ktc_value")]
    public int? KtcValue { get; set; }

    /// <summary>
    /// Fantasy Calculator player ID for linking to Fantasy Calc API data
    /// </summary>
    [Column("fantasy_calc_player_id")]
    [JsonPropertyName("fantasy_calc_player_id")]
    public int? FantasyCalcPlayerId { get; set; }

    /// <summary>
    /// Fantasy Calculator redraft value for the player
    /// </summary>
    [Column("fantasy_calc_redraft_value")]
    [JsonPropertyName("fantasy_calc_redraft_value")]
    public int? FantasyCalcRedraftValue { get; set; }

    // Audit fields
    /// <summary>
    /// When the record was created
    /// </summary>
    [Column("created_at")]
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the record was last updated
    /// </summary>
    [Column("updated_at")]
    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties (not stored in DB but useful for queries)
    /// <summary>
    /// Associated team information
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    public Team? Team { get; set; }

    /// <summary>
    /// Associated position information
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    public Position? Position { get; set; }

    // Computed properties
    /// <summary>
    /// Full name combining first and last name
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    public string FullName => $"{FirstName} {LastName}".Trim();

    /// <summary>
    /// Display name with position (e.g., "Christian McCaffrey (RB)")
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    public string DisplayName => Position != null ? $"{FullName} ({Position.Name})" : FullName;
}

/// <summary>
/// Represents a position in the Supabase Positions table
/// </summary>
[Table("Positions")]
public class Position : BaseModel
{
    /// <summary>
    /// Primary key - Auto-generated identity
    /// </summary>
    [PrimaryKey("id")]
    [Column("id")]
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>
    /// Position name (e.g., QB, RB, WR, TE)
    /// </summary>
    [Column("name")]
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// When the record was created
    /// </summary>
    [Column("created_at")]
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Represents a team in the Supabase Teams table
/// </summary>
[Table("Teams")]
public class Team : BaseModel
{
    /// <summary>
    /// Primary key
    /// </summary>
    [PrimaryKey("id")]
    [Column("id")]
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Team abbreviation (e.g., SF, DAL, GB)
    /// </summary>
    [Column("abbreviation")]
    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; } = string.Empty;

    /// <summary>
    /// Full team name (e.g., San Francisco 49ers)
    /// </summary>
    [Column("full_name")]
    [JsonPropertyName("full_name")]
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// When the record was created
    /// </summary>
    [Column("created_at")]
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the record was last updated
    /// </summary>
    [Column("updated_at")]
    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Data Transfer Object for creating or updating players
/// </summary>
public class PlayerDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public long TeamId { get; set; }
    public long? PositionId { get; set; }
    public decimal? Age { get; set; }
    public bool? Active { get; set; }
    public int? PositionalRank { get; set; }
    public int? OverallRank { get; set; }
    public string? EspnPlayerId { get; set; }
    public long? FantasySharksPlayerId { get; set; }
    public long? ProFootballReferenceId { get; set; }
    public string? PfrPlayerCode { get; set; }
    public long? SleeperId { get; set; }
    public string? KtcPlayerId { get; set; }
    public string? KtcPlayerLink { get; set; }
    public int? KtcValue { get; set; }
    public int? FantasyCalcPlayerId { get; set; }
}

/// <summary>
/// Filtering and search criteria for querying players
/// </summary>
public class PlayerSearchCriteria
{
    /// <summary>
    /// Filter by first name (partial match)
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    /// Filter by last name (partial match)
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    /// Filter by full name (partial match)
    /// </summary>
    public string? FullName { get; set; }

    /// <summary>
    /// Filter by team ID
    /// </summary>
    public long? TeamId { get; set; }

    /// <summary>
    /// Filter by position ID
    /// </summary>
    public long? PositionId { get; set; }

    /// <summary>
    /// Filter by position name (e.g., "QB", "RB")
    /// </summary>
    public string? PositionName { get; set; }

    /// <summary>
    /// Filter by team abbreviation (e.g., "SF", "DAL")
    /// </summary>
    public string? TeamAbbreviation { get; set; }

    /// <summary>
    /// Filter by active status
    /// </summary>
    public bool? Active { get; set; }

    /// <summary>
    /// Filter by ESPN player ID
    /// </summary>
    public string? EspnPlayerId { get; set; }

    /// <summary>
    /// Filter by Sleeper ID
    /// </summary>
    public long? SleeperId { get; set; }

    /// <summary>
    /// Filter by KTC player ID
    /// </summary>
    public string? KtcPlayerId { get; set; }

    /// <summary>
    /// Filter by Fantasy Calculator player ID
    /// </summary>
    public int? FantasyCalcPlayerId { get; set; }

    /// <summary>
    /// Minimum age filter
    /// </summary>
    public decimal? MinAge { get; set; }

    /// <summary>
    /// Maximum age filter
    /// </summary>
    public decimal? MaxAge { get; set; }

    /// <summary>
    /// Minimum overall rank
    /// </summary>
    public int? MinOverallRank { get; set; }

    /// <summary>
    /// Maximum overall rank
    /// </summary>
    public int? MaxOverallRank { get; set; }

    /// <summary>
    /// Page number for pagination (1-based)
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Number of records per page
    /// </summary>
    public int PageSize { get; set; } = 50;

    /// <summary>
    /// Order by field
    /// </summary>
    public string? OrderBy { get; set; }

    /// <summary>
    /// Order direction (ASC or DESC)
    /// </summary>
    public string OrderDirection { get; set; } = "ASC";
}

/// <summary>
/// Response object for paginated player queries
/// </summary>
public class PlayerSearchResponse
{
    /// <summary>
    /// List of players matching the criteria
    /// </summary>
    public List<Player> Players { get; set; } = new();

    /// <summary>
    /// Total number of records matching the criteria
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Current page number
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Number of records per page
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Total number of pages
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    /// <summary>
    /// Whether there are more pages available
    /// </summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>
    /// Whether there are previous pages available
    /// </summary>
    public bool HasPreviousPage => Page > 1;
}

/// <summary>
/// Represents a historical trade from Fantasy Calculator in the Supabase HistoricalTrades table
/// </summary>
[Table("HistoricalTrades")]
public class HistoricalTrade : BaseModel
{
    /// <summary>
    /// Primary key - Auto-generated identity
    /// </summary>
    [PrimaryKey("id")]
    [Column("id")]
    [JsonPropertyName("id")]
    [Newtonsoft.Json.JsonProperty(DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
    public int? Id { get; set; }

    /// <summary>
    /// Fantasy Calculator trade ID (unique identifier from API)
    /// </summary>
    [Column("fantasy_calc_trade_id")]
    [JsonPropertyName("fantasy_calc_trade_id")]
    public string FantasyCalcTradeId { get; set; } = string.Empty;

    /// <summary>
    /// Date the trade was executed
    /// </summary>
    [Column("trade_date")]
    [JsonPropertyName("trade_date")]
    public DateTime TradeDate { get; set; }

    /// <summary>
    /// Number of teams in the league
    /// </summary>
    [Column("num_teams")]
    [JsonPropertyName("num_teams")]
    public int NumTeams { get; set; }

    /// <summary>
    /// Number of QBs (can be decimal for superflex)
    /// </summary>
    [Column("num_qbs")]
    [JsonPropertyName("num_qbs")]
    public int NumQbs { get; set; }

    /// <summary>
    /// PPR scoring setting
    /// </summary>
    [Column("ppr")]
    [JsonPropertyName("ppr")]
    public decimal Ppr { get; set; }

    /// <summary>
    /// Whether this is a dynasty league trade
    /// </summary>
    [Column("is_dynasty")]
    [JsonPropertyName("is_dynasty")]
    public bool IsDynasty { get; set; }

    /// <summary>
    /// Number of starter positions
    /// </summary>
    [Column("num_starters")]
    [JsonPropertyName("num_starters")]
    public int? NumStarters { get; set; }

    /// <summary>
    /// Total roster size
    /// </summary>
    [Column("roster_size")]
    [JsonPropertyName("roster_size")]
    public int? RosterSize { get; set; }

    /// <summary>
    /// TE premium scoring
    /// </summary>
    [Column("te_premium")]
    [JsonPropertyName("te_premium")]
    public decimal? TePremium { get; set; }

    /// <summary>
    /// Number of superflex positions
    /// </summary>
    [Column("num_superflex")]
    [JsonPropertyName("num_superflex")]
    public int? NumSuperflex { get; set; }

    /// <summary>
    /// Site-specific league identifier from the fantasy platform
    /// </summary>
    [Column("site_league_id")]
    [JsonPropertyName("site_league_id")]
    public string? SiteLeagueId { get; set; }

    /// <summary>
    /// When the record was created
    /// </summary>
    [Column("created_at")]
    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// When the record was last updated
    /// </summary>
    [Column("updated_at")]
    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Represents a player or asset in a historical trade
/// </summary>
[Table("HistoricalTradeAssets")]
public class HistoricalTradeAsset : BaseModel
{
    /// <summary>
    /// Primary key - Auto-generated identity
    /// </summary>
    [PrimaryKey("id")]
    [Column("id")]
    [JsonPropertyName("id")]
    [Newtonsoft.Json.JsonProperty(DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
    public int? Id { get; set; }

    /// <summary>
    /// Foreign key to HistoricalTrades table
    /// </summary>
    [Column("historical_trade_id")]
    [JsonPropertyName("historical_trade_id")]
    public int HistoricalTradeId { get; set; }

    /// <summary>
    /// Foreign key to Players table (nullable for draft picks)
    /// </summary>
    [Column("player_id")]
    [JsonPropertyName("player_id")]
    public int? PlayerId { get; set; }

    /// <summary>
    /// Which side of the trade (1 or 2)
    /// </summary>
    [Column("side")]
    [JsonPropertyName("side")]
    public int Side { get; set; }

    /// <summary>
    /// When the record was created
    /// </summary>
    [Column("created_at")]
    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }
}
