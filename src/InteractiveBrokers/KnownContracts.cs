namespace InteractiveBrokers;

public static class KnownContracts
{
    public static SortedList<string, float> FutureMultiplier = new SortedList<string, float>();

    static KnownContracts()
    {
        // Initialize known future multipliers
        FutureMultiplier.Add("ES", 50f); // E-mini S&P 500
        FutureMultiplier.Add("MES", 5f); // Micro E-mini S&P 500
        FutureMultiplier.Add("NQ", 20f); // E-mini NASDAQ-100
        FutureMultiplier.Add("CL", 1000f); // Crude Oil
        FutureMultiplier.Add("GC", 100f); // Gold
        FutureMultiplier.Add("SI", 5000f); // Silver
        FutureMultiplier.Add("ZB", 10000f); // U.S. Treasury Bond
        FutureMultiplier.Add("ZN", 1000f); // U.S. Treasury Note
        FutureMultiplier.Add("RTY", 50f); // E-mini Russell 2000
    }
}
