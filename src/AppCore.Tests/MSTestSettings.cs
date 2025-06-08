using AppCore.Extenstions;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

[TestClass]
public sealed class MSTestSettings
{
    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext context)
    {
        for (int year = 2010; year <= 2050; year++) {
            TimeExtensions.LoadHolidays(year);
        }
    }
}
