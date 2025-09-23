using System.Text.Json.Serialization;

namespace ESPNScrape.Models.Espn;

/// <summary>
/// ESPN API Schedule Response Models
/// Based on ESPN's sports.core.api.espn.com endpoints
/// </summary>
public class EspnScheduleResponse
{
    [JsonPropertyName("$meta")]
    public EspnMeta Meta { get; set; } = new();

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("pageIndex")]
    public int PageIndex { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("pageCount")]
    public int PageCount { get; set; }

    [JsonPropertyName("items")]
    public List<EspnEventReference> Items { get; set; } = new();
}

public class EspnMeta
{
    [JsonPropertyName("parameters")]
    public Dictionary<string, List<string>> Parameters { get; set; } = new();
}

public class EspnEventReference
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; } = string.Empty;
}

public class EspnOddsReference
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; } = string.Empty;
}

public class EspnEvent
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("uid")]
    public string Uid { get; set; } = string.Empty;

    [JsonPropertyName("date")]
    public DateTime Date { get; set; }

    [JsonPropertyName("status")]
    public EspnEventStatus Status { get; set; } = new();

    [JsonPropertyName("competitors")]
    public List<EspnCompetitor> Competitors { get; set; } = new();

    [JsonPropertyName("competitions")]
    public List<EspnCompetition> Competitions { get; set; } = new();

    [JsonPropertyName("links")]
    public List<EspnApiLink> Links { get; set; } = new();
}

public class EspnEventStatus
{
    [JsonPropertyName("type")]
    public EspnStatusType Type { get; set; } = new();
}

public class EspnStatusType
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("completed")]
    public bool Completed { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("detail")]
    public string Detail { get; set; } = string.Empty;

    [JsonPropertyName("shortDetail")]
    public string ShortDetail { get; set; } = string.Empty;
}

public class EspnCompetitor
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("uid")]
    public string Uid { get; set; } = string.Empty;

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("homeAway")]
    public string HomeAway { get; set; } = string.Empty;

    [JsonPropertyName("team")]
    public EspnApiTeam Team { get; set; } = new();

    [JsonPropertyName("record")]
    public EspnEventReference? Record { get; set; }
}

public class EspnApiTeam
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("uid")]
    public string Uid { get; set; } = string.Empty;

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("shortDisplayName")]
    public string ShortDisplayName { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = string.Empty;

    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;

    [JsonPropertyName("color")]
    public string Color { get; set; } = string.Empty;

    [JsonPropertyName("alternateColor")]
    public string AlternateColor { get; set; } = string.Empty;
}

public class EspnCompetition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("uid")]
    public string Uid { get; set; } = string.Empty;

    [JsonPropertyName("date")]
    public DateTime Date { get; set; }

    [JsonPropertyName("competitors")]
    public List<EspnCompetitor> Competitors { get; set; } = new();

    [JsonPropertyName("odds")]
    public EspnOddsReference? Odds { get; set; }
}

public class EspnApiLink
{
    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    [JsonPropertyName("rel")]
    public List<string> Rel { get; set; } = new();

    [JsonPropertyName("href")]
    public string Href { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("shortText")]
    public string ShortText { get; set; } = string.Empty;
}

/// <summary>
/// ESPN API Odds Response Models
/// </summary>
public class EspnOddsResponse
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("pageIndex")]
    public int PageIndex { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("pageCount")]
    public int PageCount { get; set; }

    [JsonPropertyName("items")]
    public List<EspnOdds> Items { get; set; } = new();
}

public class EspnOdds
{
    [JsonPropertyName("provider")]
    public EspnOddsProvider Provider { get; set; } = new();

    [JsonPropertyName("details")]
    public string Details { get; set; } = string.Empty;

    [JsonPropertyName("overUnder")]
    public decimal? OverUnder { get; set; }

    [JsonPropertyName("spread")]
    public decimal? Spread { get; set; }

    [JsonPropertyName("overOdds")]
    public decimal? OverOdds { get; set; }

    [JsonPropertyName("underOdds")]
    public decimal? UnderOdds { get; set; }

    [JsonPropertyName("awayTeamOdds")]
    public EspnTeamOdds AwayTeamOdds { get; set; } = new();

    [JsonPropertyName("homeTeamOdds")]
    public EspnTeamOdds HomeTeamOdds { get; set; } = new();

    [JsonPropertyName("pointSpread")]
    public EspnPointSpread PointSpread { get; set; } = new();

    [JsonPropertyName("moneyline")]
    public EspnMoneyline Moneyline { get; set; } = new();

    [JsonPropertyName("total")]
    public EspnTotal Total { get; set; } = new();

    [JsonPropertyName("open")]
    public EspnOddsTimeframe Open { get; set; } = new();

    [JsonPropertyName("close")]
    public EspnOddsTimeframe Close { get; set; } = new();

    [JsonPropertyName("current")]
    public EspnOddsTimeframe Current { get; set; } = new();

    [JsonPropertyName("link")]
    public EspnOddsLink Link { get; set; } = new();
}

public class EspnOddsTimeframe
{
    [JsonPropertyName("over")]
    public EspnOddsValue Over { get; set; } = new();

    [JsonPropertyName("under")]
    public EspnOddsValue Under { get; set; } = new();

    [JsonPropertyName("total")]
    public EspnOddsValue Total { get; set; } = new();
}

public class EspnOddsProvider
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    [JsonPropertyName("logos")]
    public List<EspnProviderLogo> Logos { get; set; } = new();
}

public class EspnProviderLogo
{
    [JsonPropertyName("href")]
    public string Href { get; set; } = string.Empty;

    [JsonPropertyName("rel")]
    public List<string> Rel { get; set; } = new();
}

public class EspnTeamOdds
{
    [JsonPropertyName("favorite")]
    public bool Favorite { get; set; }

    [JsonPropertyName("underdog")]
    public bool Underdog { get; set; }

    [JsonPropertyName("moneyLine")]
    public int? MoneyLine { get; set; }

    [JsonPropertyName("spreadOdds")]
    public decimal? SpreadOdds { get; set; }

    [JsonPropertyName("favoriteAtOpen")]
    public bool FavoriteAtOpen { get; set; }
}

public class EspnPointSpread
{
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("shortDisplayName")]
    public string ShortDisplayName { get; set; } = string.Empty;

    [JsonPropertyName("home")]
    public EspnOddsTeam Home { get; set; } = new();

    [JsonPropertyName("away")]
    public EspnOddsTeam Away { get; set; } = new();
}

public class EspnMoneyline
{
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("shortDisplayName")]
    public string ShortDisplayName { get; set; } = string.Empty;

    [JsonPropertyName("home")]
    public EspnOddsTeam Home { get; set; } = new();

    [JsonPropertyName("away")]
    public EspnOddsTeam Away { get; set; } = new();
}

public class EspnTotal
{
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("shortDisplayName")]
    public string ShortDisplayName { get; set; } = string.Empty;

    [JsonPropertyName("over")]
    public EspnOddsTeam Over { get; set; } = new();

    [JsonPropertyName("under")]
    public EspnOddsTeam Under { get; set; } = new();
}

public class EspnOddsTeam
{
    [JsonPropertyName("open")]
    public EspnOddsValue Open { get; set; } = new();

    [JsonPropertyName("close")]
    public EspnOddsValue Close { get; set; } = new();
}

public class EspnOddsValue
{
    [JsonPropertyName("line")]
    public string Line { get; set; } = string.Empty;

    [JsonPropertyName("odds")]
    public string Odds { get; set; } = string.Empty;

    [JsonPropertyName("link")]
    public EspnOddsLink Link { get; set; } = new();
}

public class EspnOddsLink
{
    [JsonPropertyName("rel")]
    public List<string> Rel { get; set; } = new();

    [JsonPropertyName("href")]
    public string Href { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("tracking")]
    public EspnTracking Tracking { get; set; } = new();
}

public class EspnTracking
{
    [JsonPropertyName("campaign")]
    public string Campaign { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public Dictionary<string, string> Tags { get; set; } = new();
}