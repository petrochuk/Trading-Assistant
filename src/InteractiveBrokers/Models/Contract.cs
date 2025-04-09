namespace InteractiveBrokers.Models;

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
