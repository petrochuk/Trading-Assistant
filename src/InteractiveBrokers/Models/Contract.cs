using AppCore;
using System.Diagnostics;

namespace InteractiveBrokers.Models;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

[DebuggerDisplay("{symbol} ({conid}, {expirationDate})")]
public class Contract
{
    public string symbol { get; set; }
    public string name { get; set; }
    public AssetClass assetClass { get; set; }
    public int conid { get; set; }
    public List<ExchangeContract> contracts { get; set; } = [];
    public int underlyingConid { get; set; }
    public int expirationDate { get; set; }
    public int ltd { get; set; }
    public int shortFuturesCutOff { get; set; }
    public int longFuturesCutOff { get; set; }
}

public class ExchangeContract
{
    public int conid { get; set; }
    public string exchange { get; set; }
    public bool isUS { get; set; }
}
