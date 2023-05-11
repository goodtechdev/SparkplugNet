using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using SparkplugNet.Core;
using SparkplugNet.VersionB.Data;

namespace Goodtech.JSON
{
    /// <summary>
    /// A class used for storing known metrics, known devices and known nodes on the sparkplug B broker.
    /// </summary>
    public class StoredStates
    {
        /// <summary>
        /// Empty constructor initializes the class with a dummy metric. Used if there are no metrics in the source when starting the application.
        /// TODO: In use?
        /// </summary>
        public StoredStates()
        {
            Timestamp = DateTime.Now;
            KnownMetrics = new Dictionary<string, Metric>
            {
                { "Dummy", new Metric("Node Control/Rebirth", DataType.Boolean, true, DateTime.Now) }
            };
        }
        /// <summary>
        /// Initializes the class with a list of metrics. These will be written to the local configuration file later.
        /// </summary>
        /// <param name="metrics">The enumerable of metrics that should be added to the configuration.</param>
        public StoredStates(IEnumerable<Metric> metrics)
        {
            Timestamp = DateTime.Now;
            KnownMetrics = new Dictionary<string, Metric>();
            foreach (var metric in metrics.Where(metric => !KnownMetrics.ContainsKey(metric.Name)))
                KnownMetrics.Add(metric.Name, metric);
        }

        public DateTime Timestamp { get; set; }
        /// <summary>
        /// The known metrics on the sparkplug B broker.
        /// The metrics are stored with their name as key, and the <see cref="Metric"/> object as value.
        /// TODO: Node+Device-string could be appended in front of the metric key.
        /// </summary>
        public Dictionary<string, Metric> KnownMetrics { get; set; }
        public ConcurrentDictionary<string, MetricState<Metric>>? DeviceStates { get; set; }
        public ConcurrentDictionary<string, MetricState<Metric>>? NodeStates { get; set; }
    }
}