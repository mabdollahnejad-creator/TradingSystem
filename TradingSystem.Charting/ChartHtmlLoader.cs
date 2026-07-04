using System.IO;
using System.Reflection;

namespace TradingSystem.Charting
{
    public static class ChartHtmlLoader
    {
        public static string GetChartHtml()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "TradingSystem.Presentation.Resources.Chart.html";

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}