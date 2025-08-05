using AppCore.Models;
using System.Text.Json;

namespace InteractiveBrokers.Tests
{
    [TestClass]
    public sealed class PositionTests
    {
        [TestMethod]
        [DataRow("""{"acctId":"U*******","conid":"784136636","contractDesc":"ES     JUN2025 6040 C (E2D)","position":-1.0,"mktPrice":17.543211,"mktValue":-877.16,"currency":"USD","avgCost":1761.08,"avgPrice":35.2216,"realizedPnl":0.0,"unrealizedPnl":883.92,"exchs":null,"expiry":null,"putOrCall":null,"multiplier":null,"strike":0.0,"exerciseStyle":null,"conExchMap":[],"assetClass":"FOP","undConid":0,"model":""}""",
            2025, 6, 12, 16, 0)]
        [DataRow("""{"acctId":"U*******","conid":"784391272","contractDesc":"ES     JUN2025 6010 C (EW2)","position":-1.0,"mktPrice":28.3278103,"mktValue":-1416.39,"currency":"USD","avgCost":1448.58,"avgPrice":28.9716,"realizedPnl":0.0,"unrealizedPnl":32.19,"exchs":null,"expiry":null,"putOrCall":null,"multiplier":null,"strike":0.0,"exerciseStyle":null,"conExchMap":[],"assetClass":"FOP","undConid":0,"model":""}""",
            2025, 6, 13, 16, 0)]
        [DataRow("""{"acctId":"U*******","conid":"785625295","contractDesc":"ZN     JUN2025 112.75 C (ZN2)","position":-1.0,"mktPrice":0.0578679,"mktValue":-57.87,"currency":"USD","avgCost":45.155,"avgPrice":0.045155,"realizedPnl":0.0,"unrealizedPnl":-12.71,"exchs":null,"expiry":null,"putOrCall":null,"multiplier":null,"strike":0.0,"exerciseStyle":null,"conExchMap":[],"assetClass":"FOP","undConid":0,"model":""}""",
            2025, 6, 13, 16, 0)]
        [DataRow("""{"acctId":"U*******","conid":"767939135","contractDesc":"SPXU   JUN2025 40 C [SPXU  250620C00040000 100]","position":-5.0,"mktPrice":8.5215E-4,"mktValue":-0.43,"currency":"USD","avgCost":0.0,"avgPrice":0.0,"realizedPnl":0.0,"unrealizedPnl":-0.43,"exchs":null,"expiry":null,"putOrCall":null,"multiplier":null,"strike":0.0,"exerciseStyle":null,"conExchMap":[],"assetClass":"OPT","undConid":0,"model":""}""",
            2025, 6, 20, 16, 0)]
        public void ParsePosition(string positionJson, int expectedYear, int expectedMonth, int expectedDay, int expectedHour, int expectedMinute) {

            // Act
            var position = JsonSerializer.Deserialize(positionJson, SourceGeneratorContext.Default.Position) as IPosition;

            // Asset
            Assert.IsNotNull(position, "Position should not be null after deserialization.");
            Assert.IsNotNull(position.Expiration, "Expiration should not be null.");
            Assert.AreEqual(expectedYear, position.Expiration.Value.Year, "Expiration year does not match expected value.");
            Assert.AreEqual(expectedMonth, position.Expiration.Value.Month, "Expiration month does not match expected value.");
            Assert.AreEqual(expectedDay, position.Expiration.Value.Day, "Expiration day does not match expected value.");
            Assert.AreEqual(expectedHour, position.Expiration.Value.Hour, "Expiration hour does not match expected value.");
            Assert.AreEqual(expectedMinute, position.Expiration.Value.Minute, "Expiration minute does not match expected value.");
        }
    }
}
