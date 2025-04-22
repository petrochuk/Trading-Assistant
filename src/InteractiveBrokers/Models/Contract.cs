namespace InteractiveBrokers.Models;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

public class Contract
{
    public string symbol { get; set; }
    public int conid { get; set; }
    public int underlyingConid { get; set; }
    public int expirationDate { get; set; }
    public int ltd { get; set; }
    public int shortFuturesCutOff { get; set; }
    public int longFuturesCutOff { get; set; }
}
