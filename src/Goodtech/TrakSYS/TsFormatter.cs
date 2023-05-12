using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using ETS.Core.Api.Models.Data;
using ETS.Core.Enums;
using Goodtech.Config;
using Goodtech.Log;
using Goodtech.SparkplugB;
using Serilog;
using SparkplugNet.VersionB.Data;
using DataType = ETS.Core.Enums.DataType;

namespace Goodtech.TrakSYS
{
    public static class TsFormatter
    {
        private const char NewSeparator = '/';
        private const char OldSeparator = '.';
        /// <summary>
        ///     Adapts the datatype to or from the TrakSYS datatypes and spB Metrics
        /// </summary>
        /// <param name="datatype">The integer datatype.</param>
        public static uint AdaptDatatype(dynamic datatype)
        {

            if (datatype is SparkplugNet.VersionB.Data.DataType)
            {
                if (Constants.MetricToTs.ContainsKey(datatype)) return (uint)Constants.MetricToTs[datatype];
                throw new Exception("Datatype: " + datatype + " does not exist in configuration.");
            }

            if (datatype is DataType)
            {
                if (Constants.TsToMetric.ContainsKey(datatype)) return (uint)Constants.TsToMetric[datatype];
                throw new Exception("Datatype: " + datatype + " does not exist in configuration.");
            }

            throw new ArgumentException("Invalid datatype argument.");
        }

        /// <summary>
        ///     Parses a string to be more compatible with ISA95.
        /// </summary>
        /// <param name="list"></param>
        /// <param name="_config"></param>
        public static void ParseIsa95(List<Metric> list, ClientConfiguration _config)
        {
            const char newSeparator = '/';
            char[] oldSeparators = { '.' };

            // BELOW: Dynamic method with structured class.
            foreach (var metric in list)
            {
                var name = metric.Name; //Get the metric name
                foreach (var separator in oldSeparators) //Replace every old separator
                    if (name.Contains(separator.ToString()))
                        name = name.Replace(separator, newSeparator); //Replace old separator with new.
                var parts = name.Split(newSeparator); //Split the string based on separators
                if (parts.Length >= 6)
                {
                    for (var i = 0; i < parts.Length; i++) //For every substring, place them in the structure
                    {
                        switch (i)
                        {
                            case 0:
                                TagStructureIsa95.Enterprise = parts[i];
                                break;
                            case 1:
                                TagStructureIsa95.Site = parts[i];
                                break;
                            case 2:
                                TagStructureIsa95.Area = parts[i];
                                break;
                            case 4:
                                TagStructureIsa95.Line = parts[i];
                                break;
                            case 5:
                                TagStructureIsa95.Cell = parts[i];
                                break;
                            default:
                                TagStructureIsa95.LocalNamespaces.Add(parts[i]);
                                break;
                        }
                    }

                    metric.Name = string.Concat( //Concatenate parts
                        TagStructureIsa95.GeneratePrefix1(),
                        @"/",
                        "_" + _config.MqttNodeId,
                        TagStructureIsa95.GenerateVariablePath()
                    );
                    return;
                }
                else
                {
                    if (name.Contains(newSeparator.ToString()))
                    {
                        var indexPrefix2 = name.LastIndexOf(newSeparator);
                        TagStructureIsa95.Prefix1 = name.Substring(0, indexPrefix2);
                        TagStructureIsa95.VariablePath = name.Substring(indexPrefix2, name.Length - indexPrefix2);
                        metric.Name = string.Concat(
                            TagStructureIsa95.Prefix1,
                            "/",
                            "_" + _config.MqttNodeId,
                            TagStructureIsa95.VariablePath
                        );
                        return;
                    }
                    else
                    {
                        metric.Name = "_" + _config.MqttNodeId + "/" + name;
                    }
                }
            }
        }

        // Structured class for tag naming ISA95
        private static class TagStructureIsa95
        {
            public static string Enterprise { get; set; } = "";
            public static string Site { get; set; } = "";
            public static string Area { get; set; } = "";
            public static string Line { get; set; } = "";
            public static string Cell { get; set; } = "";
            public static string VariablePath { get; set; } = "";
            public static string Prefix1 { get; set; } = "";
            public static List<string> LocalNamespaces { get; } = new List<string>();

            public static string GeneratePrefix1()
            {
                Prefix1 = string.Concat(Enterprise, @"\", Site, @"\", Area, @"\", Line, @"\", Cell);
                return Prefix1;
            }

            public static string GenerateVariablePath()
            {
                foreach (var substring in LocalNamespaces) VariablePath = string.Concat(VariablePath, @"\", substring);
                return VariablePath;
            }
        }
        /// <summary>
        /// Transforms a metric to a TS Tag.
        /// </summary>
        /// <param name="metric">The metric to be transformed.</param>
        /// <returns></returns>
        public static DbTag TransformMetricToTag(Metric metric)
        {
            try
            {
                var tag = new DbTag
                {
                    Name = metric.Name.Replace('/', '.'),
                    DataType = (DataType)TsFormatter.AdaptDatatype(metric.DataType),
                    Type = TagType.Virtual,
                    Value = Convert.ToString(metric.Value)
                };
                //tag.UpdateDateTime = Convert.ToDateTime(metric.Timestamp);
                //Check that Quality is included
                if (metric.Properties!.Keys.Contains("Quality"))
                    tag.Quality = (int)metric.Properties.Values[metric.Properties.Keys.IndexOf("Quality")].IntValue;
                return tag;
            }
            catch (Exception e)
            {
                Logger.Error("An error occured while transforming a metric to tag: " +
                           "Name" + metric.Name + "\n" +
                           "Value" + metric.Value +"\n" +
                           "Datatype" + metric.DataType + "\n"
                           + "\n" + e.Message + "\n" + e.Data);
                throw;
            }
            
        }

        
        /// <summary>
        ///     Transforms a metric to a TS Virtual Tag.
        /// </summary>
        /// <param name="metric">The metric to be transformed.</param>
        /// <returns>A TrakSYS Virtual Tag</returns>
        public static DbTagVirtualComposite TransformMetricToTagVirtual(Metric metric)
        {
            try
            {
                var tag = new DbTagVirtualComposite
                {
                    Name = metric.Name.Replace('/', '.'),
                    DataType = (DataType)TsFormatter.AdaptDatatype(metric.DataType),
                    Value = Convert.ToString(metric.Value)
                };
                //tag.UpdateDateTime = Convert.ToDateTime(metric.Timestamp);
                //Check that Quality is included
                if (metric.Properties!.Keys.Contains("Quality"))
                    tag.Quality = (int)metric.Properties.Values[metric.Properties.Keys.IndexOf("Quality")].IntValue;
                return tag;
            }
            catch (Exception e)
            {
                Logger.Error("An error occured while transforming a metric to tag: " +
                           "Name" + metric.Name + "\n" +
                           "Value" + metric.Value +"\n" +
                           "Datatype" + metric.DataType + "\n"
                           + "\n" + e.Message + "\n" + e.Data);
                throw;
            }
        }
        /// <summary>
        ///     Converts a single tag to a <see cref="Metric" />
        /// </summary>
        /// <param name="tag">The tag that should be converted.</param>
        /// <returns>A single <see cref="Metric" /> instance.</returns>
        public static Metric TransformTagToMetric(DbTag tag)
        {
            if (tag.Value == "0" && tag.DataType == DataType.Boolean) tag.Value = "false";
            if (tag.Value == "1" && tag.DataType == DataType.Boolean) tag.Value = "true";
            var value = Convert.ChangeType(tag.Value, EnumToType(tag.DataType));
            Metric metric = new Metric(
                tag.Name.Replace(OldSeparator, NewSeparator), 
                (SparkplugNet.VersionB.Data.DataType)TsFormatter.AdaptDatatype(tag.DataType),
                value, 
                DateTime.Now);
            metric.Properties = MetricHandler.AddQuality(tag.Quality);

            //metric.Properties.Values.Add(new PropertyValue());
            return metric;
        }
        private static class Constants
        {
            public static readonly IDictionary<DataType, SparkplugNet.VersionB.Data.DataType> TsToMetric =
                new Dictionary<DataType, SparkplugNet.VersionB.Data.DataType>
                {
                    { DataType.Double, SparkplugNet.VersionB.Data.DataType.Double },
                    { DataType.Boolean, SparkplugNet.VersionB.Data.DataType.Boolean },
                    { DataType.String, SparkplugNet.VersionB.Data.DataType.String },
                    { DataType.Integer, SparkplugNet.VersionB.Data.DataType.Int32 }
                };

            public static readonly IDictionary<SparkplugNet.VersionB.Data.DataType, DataType> MetricToTs =
                new Dictionary<SparkplugNet.VersionB.Data.DataType, DataType>
                {
                    { SparkplugNet.VersionB.Data.DataType.Double, DataType.Double },
                    { SparkplugNet.VersionB.Data.DataType.Float, DataType.Double },
                    { SparkplugNet.VersionB.Data.DataType.Boolean, DataType.Boolean },
                    { SparkplugNet.VersionB.Data.DataType.String, DataType.String },
                    { SparkplugNet.VersionB.Data.DataType.Int32, DataType.Integer },
                    { SparkplugNet.VersionB.Data.DataType.Int64 , DataType.Double},
                    { SparkplugNet.VersionB.Data.DataType.Unknown , DataType.String}
                };
        }

        /// <summary>
        /// Transforms a generic object to type <see cref="DbTag"/>
        /// </summary>
        /// <param name="obj">The object that should be transformed.</param>
        /// <returns>A DbTag with the same values as the object.</returns>
        public static DbTag TransformObjectToTag(object obj)
        {
            var tag = new DbTag();
            foreach (var prop in tag.GetType().GetProperties())
            {
                var propSource = obj.GetType().GetProperty(prop.Name); 
                prop.SetValue(tag, propSource?.GetValue(obj));
            }
            return tag;
        }
        /// <summary>
        ///     Translates TrakSYS Datatype Enum to System datatypes.
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