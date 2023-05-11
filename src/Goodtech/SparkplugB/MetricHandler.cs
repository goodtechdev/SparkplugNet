using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ETS.Core.Api.Models.Data;
using ETS.Core.Enums;
using Goodtech.JSON;
using Goodtech.Log;
using Serilog;
using SparkplugNet.VersionB.Data;
using DataType = ETS.Core.Enums.DataType;
using Goodtech.TrakSYS;

namespace Goodtech.SparkplugB
{
    public static class MetricHandler
    {


        /// <summary>
        /// Checks if a metric has the updated value compared to the stored value.
        /// </summary>
        /// <param name="rxMetric"></param>
        /// <param name="applicationConfig"></param>
        /// <returns>True: if value is new. False: if value is the same.</returns>
        public static bool IsMetricValueNew(Metric rxMetric, StoredStates applicationConfig)
        {
            //Does metric exist in configuration?
            if (!applicationConfig.KnownMetrics.ContainsKey(rxMetric.Name)) return false;
            try
            {
                //Return true if value is new.
                return !applicationConfig.KnownMetrics[rxMetric.Name]!.Value!.Equals(rxMetric.Value);
            }
            catch (Exception ex)
            {
                //Logger.Error("Exception when comparing the following metric: " + rxMetric.Name + ". Value: " +
                //             rxMetric.Value + ". Datatype: " + rxMetric.DataType);
                //Logger.Error(ex.Message);
                return false;
            }
            
            
        }

        /// <summary>
        /// Validates the incoming metric against the application configuration, and appends the metric to the
        /// multi-threaded collection of metrics with updated values that should be pushed to MES.
        /// </summary>
        /// <param name="metric">The metric that should be validated.</param>
        /// <param name="applicationConfig"></param>
        /// <param name="metricsOutdated"></param>
        public static void ValidateAndAddToExistingMetrics(
            Metric metric, StoredStates applicationConfig, ConcurrentBag<Metric> metricsOutdated)
        {
            var knownMetrics = applicationConfig.KnownMetrics;
            if (!knownMetrics.ContainsKey(metric.Name)) return;
            
            //Is new metric value different from stored value?
            if (knownMetrics[metric.Name]!.Value!.Equals(metric.Value)) return;
            
            Logger.Debug("New metric value: " + metric.Name + " -> Adding value.");

            var element = metricsOutdated.LastOrDefault(item => item.Name == metric.Name);
            if (element != null)
                TransferMetricProperties(metric, element);
                //TransferEssentialMetricProperties(metric, element);
            else metricsOutdated.Add(metric);
            knownMetrics[metric.Name] = metric;
        }

        /// <summary>
        /// Checks incoming metrics against configuration, and adds new metrics to configuration.
        /// </summary>
        /// <param name="incomingMetrics">A list of incoming metrics.</param>
        /// <param name="queuedMetrics"></param>
        /// <param name="applicationConfig"></param>
        public static Dictionary<string, Metric> ValidateAndAddNewMetrics(IEnumerable<Metric> incomingMetrics, IEnumerable<Metric> queuedMetrics, StoredStates applicationConfig)
        { 
            
            var flag = false;
            var newMetrics = new Dictionary<string, Metric>();
            var knownMetrics = applicationConfig.KnownMetrics;
            var queuedMetricsList = queuedMetrics.ToList();
            //Select elements that aren't already in the queue
            var unassignedNewMetrics = incomingMetrics.Where(metric => !queuedMetricsList.Contains(metric)).ToList();
            
            foreach (var metric in unassignedNewMetrics)
            {
                //Ignore null metrics or SpB Metrics
                if (metric == null ||
                    Metrics.SparkplugBMetrics.Any(x => x.Name.Equals(metric.Name)))
                {
                    continue;
                }
                
                //If metric doesn't already exists in configuration
                
                if (!knownMetrics.ContainsKey(metric.Name) || queuedMetricsList.Contains(metric)) 
                {
                    Logger.Information("New metric received. Adding to queue.");
                    newMetrics.Add(metric.Name, metric);
                    flag = true;
                }
                else if (knownMetrics[metric.Name].Value != metric.Value) //Metric exists, but value is outdated.
                {
                    Logger.Information("New metric value received in birth. Adding to config with updated value: " + metric.Name);
                    //TODO: Endre til setValue
                    knownMetrics[metric.Name] = metric;
                }
            }

            if (flag)
            {
                Logger.Warning("Metric configuration is outdated. Recommend restart application.");
            }

            return newMetrics;
        }

        /// <summary>
        /// Transfers all properties from the source Metric object to the target Metric object.
        /// </summary>
        /// <remarks>
        /// This method uses reflection to transfer the properties from the source object to the target object.
        /// </remarks>
        /// <param name="source">The Metric object to copy properties from.</param>
        /// <param name="target">The Metric object to copy properties to.</param>
        public static void TransferMetricProperties(Metric source, Metric target)
        {
            foreach (var prop in source.GetType().GetProperties())
            {
                var name = prop.Name;
                var targetProp = target.GetType().GetProperty(name);
                if (!name.Contains("Value"))
                {
                    var value = prop.GetValue(source);
                    targetProp!.SetValue(target, prop.GetValue(source));
                }
                else
                {
                    target.SetValue(source.DataType, source.Value);
                }
            }
        }

        /// <summary>
        /// Transfers only the essential properties from the source Metric object to the target Metric object.
        /// </summary>
        /// <remarks>
        /// This method uses reflection to transfer only the essential properties (Name, Value, and Timestamp) from 
        /// the source object to the target object.
        /// </remarks>
        /// <param name="source">The Metric object to copy properties from.</param>
        /// <param name="target">The Metric object to copy properties to.</param>
        public static void TransferEssentialMetricProperties(Metric source, Metric target)
        {
            var essentialProps = new List<string>
            {
                nameof(Metric.Name),
                nameof(Metric.Value),
                nameof(Metric.Timestamp)
            };
            foreach (var name in essentialProps)
            {
                var sourceProp = source.GetType().GetProperty(name);
                var value = sourceProp!.GetValue(source);
                var targetProp = target.GetType().GetProperty(name);
                targetProp!.SetValue(target, value);
            }
        }

        public static PropertySet AddQuality(int quality)
        {
            var propertySet = new PropertySet();
            var qualityVal = new PropertyValue
            {
                IntValue = Convert.ToUInt32(quality),
                DataType = SparkplugNet.VersionB.Data.DataType.Int32
            };
            propertySet.Keys.Add("Quality");
            propertySet.Values.Add(qualityVal);
            return propertySet; 
        }
        
        /// <summary>
        /// Transforms a generic object to a <see cref="Metric" /> Uses reflection to convert values.
        /// </summary>
        /// <param name="obj">The object that should be converted.</param>
        /// <param name="type">The type of the object. i.e. <see cref="DbTag" />.</param>
        /// <returns>Returns a <see cref="Metric" />.</returns>
        public static Metric GenericToMetric(object obj, Type type)
        {
            if (type == typeof(DbTag))
            {
                try
                {
                    var propName = obj.GetType().GetProperty(nameof(Metric.Name));
                    var name = propName?.GetValue(obj, null) ?? throw new InvalidOperationException();
                    var propVal = obj.GetType().GetProperty(nameof(Metric.Value));
                    var value = propVal?.GetValue(obj, null);
                    var propDt = obj.GetType().GetProperty(nameof(Metric.DataType));
                    var datatype = (DataType)propDt!.GetValue(obj, null);

                    if ((string)value! == "0" && datatype == DataType.Boolean) value = "false";
                    if ((string)value! == "1" && datatype == DataType.Boolean) value = "true";

                    value = Convert.ChangeType(value, EnumToType(datatype));

                    var metric = new Metric((string)name, (SparkplugNet.VersionB.Data.DataType)TsFormatter.AdaptDatatype(datatype), value,
                        DateTimeOffset.Now);

                    return metric;
                }
                catch (Exception ex)
                {
                    Logger.Error("Unable to find the name of the object.\n" + ex);
                }
                

            }

            return null;
        }
        /// <summary>
        /// Translates TrakSYS Datatype Enum to System data types.
        /// </summary>
        /// <param name="type">The TrakSYS datatype.</param>
        /// <returns></returns>
        private static Type EnumToType(DataType type)
        {
            var tsTypes = new Dictionary<DataType, Type>
            {
                { DataType.Double, typeof(double) },
                { DataType.Boolean, typeof(bool) },
                { DataType.String, typeof(string) },
                { DataType.Integer, typeof(int) }
            };

            return tsTypes[type];
        }
                  
    }
    
}