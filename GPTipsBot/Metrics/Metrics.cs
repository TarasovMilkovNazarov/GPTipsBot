using Prometheus;

namespace GPTipsBot
{
    public class PrometheusMetrics
    {
        public static readonly Counter ProcessedItemsCounter = Metrics.CreateCounter("myapp_processed_items_total", "Total number of processed items.");
    }
}
