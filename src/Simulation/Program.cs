namespace Simulation
{
    internal class Program
    {
        static void Main(string[] args) {
            Console.WriteLine("Trading simulation!");

            var simulation = new GammaScalping();
            simulation.Run();
        }
    }
}
