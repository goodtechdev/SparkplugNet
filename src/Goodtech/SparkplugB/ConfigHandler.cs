using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Goodtech.JSON;
using Newtonsoft.Json;
using SparkplugNet.VersionB;
using SparkplugNet.VersionB.Data;

namespace Goodtech.SparkplugB
{
    public static class ConfigHandler
    {
        /// <summary>
        ///     Writes the AppConfig class to file.
        /// </summary>
        /// <param name="applicationConfig"></param>
        /// <param name="pathKnownMetrics"></param>
        public static Task SaveConfiguration(StoredStates applicationConfig, string pathKnownMetrics)
        {
            applicationConfig.Timestamp = DateTime.Now;
            File.WriteAllText(pathKnownMetrics, ""); //Delete content
            var output = JsonConvert.SerializeObject(applicationConfig); //Serialize the metric list
            File.WriteAllText(pathKnownMetrics, output); //Write all metrics back to file
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Generates an application configuration from the source MES system. The configuration is stored at a specific placement as a JSON-file.
        /// This is later used as a template when initializing the MQTT Client. 
        /// </summary>
        /// <param name="mesCollection">A list of metrics from MES.</param>
        /// <param name="pathKnownMetrics"></param>
        /// <returns>An application configuration.</returns>
        /// <exception cref="NullReferenceException"></exception>
        public static StoredStates GenerateAppConfiguration(List<Metric> mesCollection, string pathKnownMetrics)
        {
            StoredStates applicationConfig;
            //var metricCollection = new Dictionary<string, Metric>();
            applicationConfig = new StoredStates(mesCollection);
            SaveConfiguration(applicationConfig, pathKnownMetrics);
            
            // //Create file and fill content if this doesn't exist
            // if (!File.Exists(pathKnownMetrics))
            // {
            //     //File.Create(pathKnownMetrics);
            //     applicationConfig = new StoredStates(mesCollection);
            //     SaveConfiguration(applicationConfig, pathKnownMetrics);
            // }
            //
            // //Get the app configuration from file
            // applicationConfig = JsonConvert.DeserializeObject<StoredStates>(File.ReadAllText(pathKnownMetrics))
            //                          ?? throw new NullReferenceException(pathKnownMetrics + " not found. If the file exists, delete it, and try again."); 
            //
            
            //Add all metrics from TrakSYS
            //var metricCollection = mesCollection.ToDictionary(metric => metric.Name);
            // foreach (var metric in mesCollection)
            // {
            //     if (metricCollection.ContainsKey(metric.Name))
            //     {
            //         metricCollection[metric.Name] = metric; continue;
            //     }
            //     else metricCollection.Add(metric.Name, metric);
            // }
            
            //Add unique metrics from knownMetrics
            // foreach (var metric in applicationConfig.KnownMetrics)
            // {
            //     if (metricCollection.ContainsKey(metric.Key)) continue;
            //     else metricCollection.Add(metric.Key, metric.Value);
            // }

            //Add SparkplugMetrics
            foreach (var metric in Metrics.SparkplugBMetrics)
            {
                if (applicationConfig.KnownMetrics.ContainsKey(metric.Name)) continue;
                else applicationConfig.KnownMetrics.Add(metric.Name, metric);
            }
            
            //Replace the old dictionary, but return the old object.
            //applicationConfig.KnownMetrics = metricCollection;

            return applicationConfig;
        }
        
    }
}