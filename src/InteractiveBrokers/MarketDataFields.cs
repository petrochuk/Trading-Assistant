namespace InteractiveBrokers;

public enum MarketDataFields
{
    /// <summary>
    /// The last price at which the contract traded. May contain one of the following prefixes: C – Previous day’s closing price. H – Trading has halted.
    /// </summary>
    LastPrice = 31,

    /// <summary>
    /// Symbol of the contract.
    /// </summary>
    Symbol = 55,

    /// <summary>
    /// Text
    /// </summary>
    Text = 58,

    /// <summary>
    /// Current day open price
    /// </summary>
    High = 70,

    /// <summary>
    /// Current day low price
    /// </summary>
    Low = 71,

    /// <summary>
    /// The current market value of your position in the security. Market Value is calculated with real time market data (even when not subscribed to market data).
    /// </summary>
    MarketValue = 73,

    /// <summary>
    /// The average price of the position.
    /// </summary>
    AvgPrice = 74,

    /// <summary>
    /// Unrealized profit or loss. Unrealized PnL is calculated with real time market data (even when not subscribed to market data).
    /// </summary>
    UnrealizedPnL = 75,

    /// <summary>
    /// Formatted position
    /// </summary>
    FormattedPosition = 76,

    /// <summary>
    /// Formatted unrealized PnL
    /// </summary>
    FormattedUnrealizedPnL = 77,

    /// <summary>
    /// Your profit or loss of the day since prior close. Daily PnL is calculated with real time market data (even when not subscribed to market data).
    /// </summary>
    DailyPnL = 78,

    /// <summary>
    /// Realized profit or loss. Realized PnL is calculated with real time market data (even when not subscribed to market data).
    /// </summary>
    RealizedPnL = 79,

    /// <summary>
    /// Unrealized profit or loss expressed in percentage.
    /// </summary>
    UnrealizedPnLPercent = 80,

    /// <summary>
    /// The difference between the last price and the close on the previous trading day.
    /// </summary>
    Change = 82,

    /// <summary>
    /// The difference between the last price and the close on the previous trading day in percentage.
    /// </summary>
    ChangePercent = 83,

    /// <summary>
    /// The highest-priced bid on the contract.
    /// </summary>
    BidPrice = 84,

    /// <summary>
    /// The number of contracts or shares bid for at the bid price. For US stocks, the number displayed is divided by 100.
    /// </summary>
    AskSize = 85,

    /// <summary>
    /// The lowest-priced offer on the contract.
    /// </summary>
    AskPrice = 86,

    /// <summary>
    /// Volume for the day, formatted with ‘K’ for thousands or ‘M’ for millions. For higher precision volume refer to field 7762.
    /// </summary>
    Volume = 87,

    /// <summary>
    /// The number of contracts or shares bid for at the bid price. For US stocks, the number displayed is divided by 100.
    /// </summary>
    BidSize = 88,

    /// <summary>
    /// Returns the right of the instrument, such as P for Put or C for Call.
    /// </summary>
    Right = 201,

    /// <summary>
    /// Exchange where the contract is traded.
    /// </summary>
    Exchange = 6004,

    /// <summary>
    /// Contract identifier from IBKR’s database.
    /// </summary>
    Conid = 6008,

    /// <summary>
    /// The asset class of the instrument.
    /// </summary>
    SecType = 6070,

    /// <summary>
    /// Months
    /// </summary>
    Months = 6072,

    /// <summary>
    /// Regular Expiry
    /// </summary>
    RegularExpiry = 6073,

    /// <summary>
    /// Marker for market data delivery method (similar to request id)
    /// </summary>
    Marker = 6119,

    /// <summary>
    /// Underlying Conid.Use /trsrv/secdef to get more information about the security
    /// </summary>
    UnderlyingConid = 6457,

    /// <summary>
    /// Service Params.
    /// </summary>
    ServiceParams = 6508,

    /// <summary>
    /// Market Data Availability.The field may contain three chars.First char defines: R = RealTime, D = Delayed, Z = Frozen, Y = Frozen Delayed, N = Not Subscribed, i – incomplete, v – VDR Exempt (Vendor Display Rule 603c). Second char defines: P = Snapshot, p = Consolidated.Third char defines: B = Book.RealTime Data is relayed back in real time without delay, market data subscription(s) are required.Delayed – Data is relayed back 15-20 min delayed. Frozen – Last recorded data at market close, relayed back in real time. Frozen Delayed – Last recorded data at market close, relayed back delayed.Not Subscribed – User does not have the required market data subscription(s) to relay back either real time or delayed data. Snapshot – Snapshot request is available for contract.Consolidated – Market data is aggregated across multiple exchanges or venues. Book – Top of the book data is available for contract.
    /// </summary>
    MarketDataAvailability = 6509,

    /// <summary>
    /// Company name
    /// </summary>
    CompanyName = 7051,

    /// <summary>
    /// Ask Exch Displays the exchange(s) offering the SMART price.A= AMEX, C= CBOE, I= ISE, X= PHLX, N= PSE, B= BOX, Q= NASDAQOM, Z= BATS, W= CBOE2, T= NASDAQBX, M= MIAX, H= GEMINI, E= EDGX, J= MERCURY
    /// </summary>
    AskExch = 7057,

    /// <summary>
    /// Last Exch Displays the exchange(s) offering the SMART price.A= AMEX, C= CBOE, I= ISE, X= PHLX, N= PSE, B= BOX, Q= NASDAQOM, Z= BATS, W= CBOE2, T= NASDAQBX, M= MIAX, H= GEMINI, E= EDGX, J= MERCURY
    /// </summary>
    LastExch = 7058,

    /// <summary>
    /// Last Size The number of unites traded at the last price
    /// </summary>
    LastSize = 7059,

    /// <summary>
    /// Bid Exch Displays the exchange(s) offering the SMART price.A= AMEX, C= CBOE, I= ISE, X= PHLX, N= PSE, B= BOX, Q= NASDAQOM, Z= BATS, W= CBOE2, T= NASDAQBX, M= MIAX, H= GEMINI, E= EDGX, J= MERCURY
    /// </summary>
    BidExch = 7068,

    /// <summary>
    /// Implied Volatility	The implied volatility for the specific strike of the option in percentage.To query the Option Implied Vol. % from the underlying refer to field 7283.
    /// </summary>
    ImpliedVolatility = 7084,

    /// <summary>
    /// Put/Call Interest Put option open interest/call option open interest for the trading day.
    /// </summary>
    PutCallInterest = 7085,

    /// <summary>
    /// Put/Call Volume Put option volume/call option volume for the trading day.
    /// </summary>
    PutCallVolume = 7086,

    /// <summary>
    /// Hist. Vol. % 30-day real-time historical volatility.
    /// </summary>
    HistVol = 7087,

    /// <summary>
    /// Hist. Vol.Close %	Shows the historical volatility based on previous close price.
    /// </summary>
    HistVolClose = 7088,

    /// <summary>
    /// Opt. Volume Option volume.
    /// </summary>
    OptVolume = 7089,

    /// <summary>
    /// Conid + Exchange
    /// </summary>
    ConidExchange = 7094,

    /// <summary>
    /// If the contract is a trade-able instrument. Returns 1(true) or 0(false).
    /// </summary>
    CanBeTraded = 7184,

    /// <summary>
    /// Contract Description
    /// </summary>
    ContractDescription = 7219,

    /// <summary>
    /// Contract Description
    /// </summary>
    ContractDescription2 = 7220,

    /// <summary>
    /// Listing Exchange
    /// </summary>
    ListingExchange = 7221,

    /// <summary>
    /// Displays the type of industry under which the underlying company can be categorized.
    /// </summary>
    Industry = 7280,

    /// <summary>
    /// Displays a more detailed level of description within the industry under which the underlying company can be categorized.
    /// </summary>
    Category = 7281,

    /// <summary>
    /// The average daily trading volume over 90 days.
    /// </summary>
    AverageVolume = 7282,

    /// <summary>
    /// A prediction of how volatile an underlying will be in the future. At the market volatility estimated for a maturity thirty calendar days forward of the current trading day, and based on option prices from two consecutive expiration months. To query the Implied Vol. % of a specific strike refer to field 7633.
    /// </summary>
    OptionImpliedVolatility = 7283,

    /// <summary>
    /// Put/Call Ratio
    /// </summary>
    PutCallRatio = 7285,

    /// <summary>
    /// Displays the amount of the next dividend.
    /// </summary>
    DividendAmount = 7286,

    /// <summary>
    /// This value is the total of the expected dividend payments over the next twelve months per share divided by the Current Price and is expressed as a percentage. For derivatives, this displays the total of the expected dividend payments over the expiry date.
    /// </summary>
    DividendYield = 7287,

    /// <summary>
    /// Ex-date of the dividend
    /// </summary>
    ExDate = 7288,

    /// <summary>
    /// Market Cap
    /// </summary>
    MarketCap = 7289,

    /// <summary>
    /// Price to Earnings Ratio
    /// </summary>
    PERatio = 7290,

    /// <summary>
    /// Earnings Per Share
    /// </summary>
    EPS = 7291,

    /// <summary>
    /// Your current position in this security multiplied by the average price and multiplier.
    /// </summary>
    CostBasis = 7292,

    /// <summary>
    /// The highest price for the past 52 weeks.
    /// </summary>
    FiftyTwoWeekHigh = 7293,

    /// <summary>
    /// The lowest price for the past 52 weeks.
    /// </summary>
    FiftyTwoWeekLow = 7294,

    /// <summary>
    /// Today’s opening price.
    /// </summary>
    Open = 7295,

    /// <summary>
    /// Today’s closing price.
    /// </summary>
    Close = 7296,

    /// <summary>
    /// The ratio of the change in the price of the option to the corresponding change in the price of the underlying.
    /// </summary>
    Delta = 7308,

    /// <summary>
    /// The rate of change for the delta with respect to the underlying asset’s price.
    /// </summary>
    Gamma = 7309,

    /// <summary>
    /// A measure of the rate of decline the value of an option due to the passage of time.
    /// </summary>
    Theta = 7310,

    /// <summary>
    /// The amount that the price of an option changes compared to a 1% change in the volatility.
    /// </summary>
    Vega = 7311,

    /// <summary>
    /// Today’s option volume as a percentage of the average option volume.
    /// </summary>
    OptVolumeChange = 7607,

    /// <summary>
    /// The implied volatility for the specific strike of the option in percentage. To query the Option Implied Vol. % from the underlying refer to field 7283.
    /// </summary>
    ImpliedVolatility2 = 7633,

    /// <summary
    /// For derivatives, displays the current price of the underlying. For example, for stock options this column displays the current stock price.
    /// </summary>
    UnderlyingPrice = 7634,

    /// <summary>
    /// The mark price is, the ask price if ask is less than last price, the bid price if bid is more than the last price, otherwise it’s equal to last price.
    /// </summary>
    MarkPrice = 7635,

    /// <summary>
    /// Shortable Shares Number of shares available for shorting.
    /// </summary>
    ShortableShares = 7636,

    /// <summary>
    /// Rate of Interest Interest rate charged on borrowed shares.
    /// </summary>
    FeeRate = 7637,

    /// <summary>
    /// Option Open Interest
    /// </summary>
    OptionOpenInterest = 7638,

    /// <summary>
    /// Displays the market value of the contract as a percentage of the total market value of the account. Mark Value is calculated with real time market data (even when not subscribed to market data).
    /// </summary>
    PercentOfMarkValue = 7639,

    /// <summary>
    /// Describes the level of difficulty with which the security can be sold short.
    /// </summary>
    Shortable = 7644,

    /// <summary>
    /// Displays Morningstar Rating provided value.Requires Morningstar subscription.
    /// </summary>
    MorningstarRating = 7655,

    /// <summary>
    /// This value is the total of the expected dividend payments over the next twelve months per share.
    /// </summary>
    Dividends = 7671,

    /// <summary>
    /// This value is the total of the expected dividend payments over the last twelve months per share.
    /// </summary>
    DividendsTTM = 7672,

    /// <summary>
    /// Exponential moving average(N= 200).
    /// </summary>
    EMA200 = 7674,

    /// <summary>
    /// Exponential moving average(N= 100).
    /// </summary>
    EMA100 = 7675,

    /// <summary>
    /// Exponential moving average(N= 50).
    /// </summary>
    EMA50 = 7676,

    /// <summary>
    /// Exponential moving average(N= 20).
    /// </summary>
    EMA20 = 7677,

    /// <summary>
    /// Price to Exponential moving average(N= 200) ratio -1, displayed in percents.
    /// </summary>
    PriceEMA200 = 7678,

    /// <summary>
    /// Price to Exponential moving average(N= 100) ratio -1, displayed in percents.
    /// </summary>
    PriceEMA100 = 7679,

    /// <summary>
    /// Price to Exponential moving average(N= 50) ratio -1, displayed in percents.
    /// </summary>
    PriceEMA50 = 7724,

    /// <summary>
    /// Price to Exponential moving average(N= 20) ratio -1, displayed in percents.
    /// </summary>
    PriceEMA20 = 7681,

    /// <summary>
    /// The difference between the last price and the open price.
    /// </summary>
    ChangeSinceOpen = 7682,

    /// <summary>
    /// Beta Weighted Delta is calculated using the formula; Delta x dollar adjusted beta, where adjusted beta is adjusted by the ratio of the close price.
    /// </summary>
    SPXDeltaBetaWeightedDelta = 7696,

    /// <summary>
    /// Total number of outstanding futures contracts.
    /// </summary>
    FuturesOpenInterest = 7697,

    /// <summary>
    /// Implied yield of the bond if it is purchased at the current market price. Implied yield is calculated using the Ask on all possible call dates. It is assumed that prepayment occurs if the bond has call or put provisions and the issuer can offer a lower coupon rate based on current market rates. The yield to worst will be the lowest of the yield to maturity or yield to call (if the bond has prepayment provisions). Yield to worse may be the same as yield to maturity but never higher.
    /// </summary>
    LastYield = 7698,

    /// <summary>
    /// Implied yield of the bond if it is purchased at the current bid price. Bid yield is calculated using the Ask on all possible call dates. It is assumed that prepayment occurs if the bond has call or put provisions and the issuer can offer a lower coupon rate based on current market rates. The yield to worst will be the lowest of the yield to maturity or yield to call (if the bond has prepayment provisions). Yield to worse may be the same as yield to maturity but never higher.
    /// </summary>
    BidYield = 7699,

    /// <summary>
    /// Beta is against standard index.
    /// </summary>
    Beta = 7718,

    /// <summary>
    /// Yesterday’s closing price.
    /// </summary>
    PriorClose = 7741,

    /// <summary>
    /// High precision volume for the day. For formatted volume refer to field 87.
    /// </summary>
    VolumeLong = 7762,

    /// <summary>
    /// If user has trading permissions for specified contract. Returns 1(true) or 0(false).
    /// </summary>
    HasTradingPermissions = 7768,

    /// <summary>
    /// Your profit or loss of the day since prior close. Daily PnL is calculated with real-time market data (even when not subscribed to market data).
    /// </summary>
    DailyPnLRaw = 7920,

    /// <summary>
    /// Your current position in this security multiplied by the average price and and multiplier.
    /// </summary>
    CostBasisRaw = 7921,
}
