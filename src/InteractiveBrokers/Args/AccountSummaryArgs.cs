using System.Text.Json.Serialization;

namespace InteractiveBrokers.Args;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

public class AccountSummaryArgs : EventArgs
{
    public SummaryLine accountcode { get; set; }
    public SummaryLine accountready { get; set; }
    public SummaryLine accounttype { get; set; }

    public SummaryLine accruedcash { get; set; }
    [JsonPropertyName("accruedcash-c")]
    public SummaryLine accruedcashc { get; set; }
    [JsonPropertyName("accruedcash-s")]
    public SummaryLine accruedcashs { get; set; }
    
    public SummaryLine accrueddividend { get; set; }
    [JsonPropertyName("accrueddividend-c")]
    public SummaryLine accrueddividendc { get; set; }
    [JsonPropertyName("accrueddividend-s")]
    public SummaryLine accrueddividends { get; set; }

    public SummaryLine availablefunds { get; set; }
    [JsonPropertyName("availablefunds-c")]
    public SummaryLine availablefundsc { get; set; }
    [JsonPropertyName("availablefunds-s")]
    public SummaryLine availablefundss { get; set; }

    public SummaryLine billable { get; set; }
    [JsonPropertyName("billable-c")]
    public SummaryLine billablec { get; set; }
    [JsonPropertyName("billable-s")]
    public SummaryLine billables { get; set; }

    public SummaryLine buyingpower { get; set; }
    [JsonPropertyName("columnprio-c")]
    public SummaryLine columnprioc { get; set; }
    [JsonPropertyName("columnprio-s")]
    public SummaryLine columnprios { get; set; }

    public SummaryLine cushion { get; set; }

    public SummaryLine daytradesremaining { get; set; }
    [JsonPropertyName("daytradesremainingt+1")]
    public SummaryLine daytradesremainingt1 { get; set; }
    [JsonPropertyName("daytradesremainingt+2")]
    public SummaryLine daytradesremainingt2 { get; set; }
    [JsonPropertyName("daytradesremainingt+3")]
    public SummaryLine daytradesremainingt3 { get; set; }
    [JsonPropertyName("daytradesremainingt+4")]
    public SummaryLine daytradesremainingt4 { get; set; }

    [JsonPropertyName("daytradingstatus-s")]
    public SummaryLine daytradingstatuss { get; set; }

    public SummaryLine depositoncredithold { get; set; }

    public SummaryLine equitywithloanvalue { get; set; }
    [JsonPropertyName("equitywithloanvalue-c")]
    public SummaryLine equitywithloanvaluec { get; set; }
    [JsonPropertyName("equitywithloanvalue-s")]
    public SummaryLine equitywithloanvalues { get; set; }

    public SummaryLine excessliquidity { get; set; }
    [JsonPropertyName("excessliquidity-c")]
    public SummaryLine excessliquidityc { get; set; }
    [JsonPropertyName("excessliquidity-s")]
    public SummaryLine excessliquiditys { get; set; }

    public SummaryLine fullavailablefunds { get; set; }
    [JsonPropertyName("fullavailablefunds-c")]
    public SummaryLine fullavailablefundsc { get; set; }
    [JsonPropertyName("fullavailablefunds-s")]
    public SummaryLine fullavailablefundss { get; set; }

    public SummaryLine fullexcessliquidity { get; set; }
    [JsonPropertyName("fullexcessliquidity-c")]
    public SummaryLine fullexcessliquidityc { get; set; }
    [JsonPropertyName("fullexcessliquidity-s")]
    public SummaryLine fullexcessliquiditys { get; set; }

    public SummaryLine fullinitmarginreq { get; set; }
    [JsonPropertyName("fullinitmarginreq-c")]
    public SummaryLine fullinitmarginreqc { get; set; }
    [JsonPropertyName("fullinitmarginreq-s")]
    public SummaryLine fullinitmarginreqs { get; set; }

    public SummaryLine fullmaintmarginreq { get; set; }
    [JsonPropertyName("fullmaintmarginreq-c")]
    public SummaryLine fullmaintmarginreqc { get; set; }
    [JsonPropertyName("fullmaintmarginreq-s")]
    public SummaryLine fullmaintmarginreqs { get; set; }

    public SummaryLine grosspositionvalue { get; set; }
    [JsonPropertyName("grosspositionvalue-s")]
    public SummaryLine grosspositionvalues { get; set; }

    public SummaryLine guarantee { get; set; }
    [JsonPropertyName("guarantee-c")]
    public SummaryLine guaranteec { get; set; }
    [JsonPropertyName("guarantee-s")]
    public SummaryLine guarantees { get; set; }

    public SummaryLine highestseverity { get; set; }

    public SummaryLine incentivecoupons { get; set; }
    [JsonPropertyName("incentivecoupons-c")]
    public SummaryLine incentivecouponsc { get; set; }
    [JsonPropertyName("incentivecoupons-s")]
    public SummaryLine incentivecouponss { get; set; }

    public SummaryLine indianstockhaircut { get; set; }
    [JsonPropertyName("indianstockhaircut-c")]
    public SummaryLine indianstockhaircutc { get; set; }
    [JsonPropertyName("indianstockhaircut-s")]
    public SummaryLine indianstockhaircuts { get; set; }

    public SummaryLine initmarginreq { get; set; }
    [JsonPropertyName("initmarginreq-c")]
    public SummaryLine initmarginreqc { get; set; }
    [JsonPropertyName("initmarginreq-s")]
    public SummaryLine initmarginreqs { get; set; }

    [JsonPropertyName("leverage-s")]
    public SummaryLine leverages { get; set; }

    public SummaryLine lookaheadavailablefunds { get; set; }
    [JsonPropertyName("lookaheadavailablefunds-c")]
    public SummaryLine lookaheadavailablefundsc { get; set; }
    [JsonPropertyName("lookaheadavailablefunds-s")]
    public SummaryLine lookaheadavailablefundss { get; set; }

    public SummaryLine lookaheadexcessliquidity { get; set; }
    [JsonPropertyName("lookaheadexcessliquidity-c")]
    public SummaryLine lookaheadexcessliquidityc { get; set; }
    [JsonPropertyName("lookaheadexcessliquidity-s")]
    public SummaryLine lookaheadexcessliquiditys { get; set; }

    public SummaryLine lookaheadinitmarginreq { get; set; }
    [JsonPropertyName("lookaheadinitmarginreq-c")]
    public SummaryLine lookaheadinitmarginreqc { get; set; }
    [JsonPropertyName("lookaheadinitmarginreq-s")]
    public SummaryLine lookaheadinitmarginreqs { get; set; }

    public SummaryLine lookaheadmaintmarginreq { get; set; }
    [JsonPropertyName("lookaheadmaintmarginreq-c")]
    public SummaryLine lookaheadmaintmarginreqc { get; set; }
    [JsonPropertyName("lookaheadmaintmarginreq-s")]
    public SummaryLine lookaheadmaintmarginreqs { get; set; }

    public SummaryLine lookaheadnextchange { get; set; }

    public SummaryLine maintmarginreq { get; set; }
    [JsonPropertyName("maintmarginreq-c")]
    public SummaryLine maintmarginreqc { get; set; }
    [JsonPropertyName("maintmarginreq-s")]
    public SummaryLine maintmarginreqs { get; set; }

    public required SummaryLine NetLiquidation { get; init; }
    [JsonPropertyName("netliquidation-c")]
    public required SummaryLine NetLiquidationC { get; init; }
    [JsonPropertyName("netliquidation-s")]
    public required SummaryLine NetLiquidationS { get; init; }

    public SummaryLine netliquidationuncertainty { get; set; }
    public SummaryLine nlvandmargininreview { get; set; }

    public SummaryLine pasharesvalue { get; set; }
    [JsonPropertyName("pasharesvalue-c")]
    public SummaryLine pasharesvaluec { get; set; }
    [JsonPropertyName("pasharesvalue-s")]
    public SummaryLine pasharesvalues { get; set; }

    public SummaryLine physicalcertificatevalue { get; set; }
    [JsonPropertyName("physicalcertificatevalue-c")]
    public SummaryLine physicalcertificatevaluec { get; set; }
    [JsonPropertyName("physicalcertificatevalue-s")]
    public SummaryLine physicalcertificatevalues { get; set; }

    public SummaryLine postexpirationexcess { get; set; }
    [JsonPropertyName("postexpirationexcess-c")]
    public SummaryLine postexpirationexcessc { get; set; }
    [JsonPropertyName("postexpirationexcess-s")]
    public SummaryLine postexpirationexcesss { get; set; }

    public SummaryLine postexpirationmargin { get; set; }
    [JsonPropertyName("postexpirationmargin-c")]
    public SummaryLine postexpirationmarginc { get; set; }
    [JsonPropertyName("postexpirationmargin-s")]
    public SummaryLine postexpirationmargins { get; set; }

    public SummaryLine previousdayequitywithloanvalue { get; set; }
    [JsonPropertyName("previousdayequitywithloanvalue-s")]
    public SummaryLine previousdayequitywithloanvalues { get; set; }

    [JsonPropertyName("segmenttitle-c")]
    public SummaryLine segmenttitlec { get; set; }
    [JsonPropertyName("segmenttitle-s")]
    public SummaryLine segmenttitles { get; set; }

    public SummaryLine totalcashvalue { get; set; }
    [JsonPropertyName("totalcashvalue-c")]
    public SummaryLine totalcashvaluec { get; set; }
    [JsonPropertyName("totalcashvalue-s")]
    public SummaryLine totalcashvalues { get; set; }

    public SummaryLine totaldebitcardpendingcharges { get; set; }
    [JsonPropertyName("totaldebitcardpendingcharges-c")]
    public SummaryLine totaldebitcardpendingchargesc { get; set; }
    [JsonPropertyName("totaldebitcardpendingcharges-s")]
    public SummaryLine totaldebitcardpendingchargess { get; set; }
    public SummaryLine tradingtypes { get; set; }
}

public class SummaryLine
{
    [JsonPropertyName("amount")]
    public float Amount { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;

    [JsonPropertyName("isNull")]
    public bool IsNull { get; set; }

    [JsonPropertyName("timestamp")]
    public long TimeStamp { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("severity")]
    public int Severity { get; set; }

    public override string ToString() {
        return $"{nameof(Amount)}: {Amount}, {nameof(Value)}: {Value}";
    }
}
