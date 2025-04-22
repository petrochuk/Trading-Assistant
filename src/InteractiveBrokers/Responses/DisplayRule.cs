namespace InteractiveBrokers.Responses;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

public class DisplayRule
{
    public int magnification { get; set; }
    public DisplayRuleStep[] displayRuleStep { get; set; }
}