using System.ComponentModel.DataAnnotations;

namespace FantasyCalcScrape.Models;

public class FantasyCalcValueResponse
{
    public bool Success { get; set; } = true;
    public string? Error { get; set; }
    public DateTime LastUpdated { get; set; }
    public List<FantasyCalcPlayer> Values { get; set; } = new();
}

public class FantasyCalcTradeResponse
{
    public bool Success { get; set; } = true;
    public string? Error { get; set; }
    public DateTime LastUpdated { get; set; }
    public List<FantasyCalcTrade> Trades { get; set; } = new();
}

public class FantasyCalcTrade
{
    public string Id { get; set; } = string.Empty;
    public string LeagueId { get; set; } = string.Empty;
    public DateTime Date { get; set; }

    public string? SiteLeagueId { get; set; }

    public List<TradePlayer> Side1 { get; set; } = new();
    public List<TradePlayer> Side2 { get; set; } = new();

    public int NumTeams { get; set; }
    public decimal NumQbs { get; set; }
    public decimal Ppr { get; set; }
    public bool IsDynasty { get; set; }
    public decimal TePremium { get; set; }
    public int NumQbsNonSuperflex { get; set; }
    public int NumSuperflex { get; set; }
    public int NumRbs { get; set; }
    public int NumWrs { get; set; }
    public int NumTes { get; set; }
    public int NumFlex { get; set; }
    public int NumStarters { get; set; }
    public int RosterSize { get; set; }
    public decimal PassTds { get; set; }

    public int? MaybeTradedValueDiff { get; set; }
    public decimal? MaybeScore { get; set; }
    public string? MaybeGrade { get; set; }
    public string? UsernameSide1 { get; set; }
    public string? UsernameSide2 { get; set; }
}

public class TradePlayer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MflId { get; set; } = string.Empty;
    public string SleeperId { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string? MaybeBirthday { get; set; }
    public string? MaybeHeight { get; set; }
    public int? MaybeWeight { get; set; }
    public string? MaybeCollege { get; set; }
    public string? MaybeTeam { get; set; }
    public decimal? MaybeAge { get; set; }
    public int? MaybeYoe { get; set; }
    public string? EspnId { get; set; }
    public string? FleaflickerId { get; set; }
}

public class FantasyCalcPlayer
{
    public Player Player { get; set; } = new();
    public int Value { get; set; }
    public int OverallRank { get; set; }
    public int PositionRank { get; set; }
    public int Trend30Day { get; set; }
    public int RedraftDynastyValueDifference { get; set; }
    public int RedraftDynastyValuePercDifference { get; set; }
    public int RedraftValue { get; set; }
    public int CombinedValue { get; set; }
    public int? MaybeMovingStandardDeviation { get; set; }
    public int? MaybeMovingStandardDeviationPerc { get; set; }
    public int? MaybeMovingStandardDeviationAdjusted { get; set; }
    public bool DisplayTrend { get; set; }
    public object? MaybeOwner { get; set; }
    public bool Starter { get; set; }
    public int? MaybeTier { get; set; }
    public decimal? MaybeAdp { get; set; }
    public decimal? MaybeTradeFrequency { get; set; }
}

public class Player
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MflId { get; set; } = string.Empty;
    public string SleeperId { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string? MaybeBirthday { get; set; }
    public string? MaybeHeight { get; set; }
    public int? MaybeWeight { get; set; }
    public string? MaybeCollege { get; set; }
    public string? MaybeTeam { get; set; }
    public decimal? MaybeAge { get; set; }
    public int? MaybeYoe { get; set; }
    public string? EspnId { get; set; }
    public string? FleaflickerId { get; set; }

    public int? FantasyCalcRedraftValue { get; set; }
}

public class FantasyCalcApiSettings
{
    public bool IsDynasty { get; set; } = false;

    [Range(1, 4)]
    public int NumQbs { get; set; } = 1;

    [Range(8, 20)]
    public int NumTeams { get; set; } = 12;

    [Range(0, 2)]
    public decimal Ppr { get; set; } = 1.0m;

    public string? Superflex { get; set; }
    public string? Te_premium { get; set; }
}