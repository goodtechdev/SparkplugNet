using System;
using System.Collections.Generic;
using SparkplugNet.VersionB.Data;

namespace Goodtech.SparkplugB
{
    /// <summary>
    /// A static class to keep an overview of the typical Sparkplug B metrics that the application should be able to send and receive.
    /// </summary>
    public static class Metrics
    {
        #region SparkplugB Metrics

        //Initial or built-in metrics that the application needs
        public static readonly Metric SpBNodeRebirth =
            new Metric("Node Control/Rebirth", DataType.Boolean, true, DateTime.Now);

        public static readonly Metric SpBDeviceRebirth =
            new Metric("Device Control/Rebirth", DataType.Boolean, true, DateTime.Now);

        public static readonly Metric SpBSessionNumber =
            new Metric("bdSeq", DataType.UInt64, 0, DateTime.Now);
        
        public static readonly Metric SpBNodeNextServer =
            new Metric("Node Control/Next Server", DataType.UInt64, 0, DateTime.Now);
        
        public static readonly Metric SpBNodeTransmissionVersion =
            new Metric("Node Info/Transmission Version", DataType.UInt64, 0, DateTime.Now);

        public static readonly List<Metric> SparkplugBMetrics = new List<Metric>
        {
            SpBNodeRebirth,
            SpBDeviceRebirth,
            SpBSessionNumber,
            SpBNodeNextServer,
            SpBNodeTransmissionVersion
        };

        #endregion
    }
}