using System.Collections.Generic;
using ETS.Core.Api.Models.Data;
using Goodtech.Config;
using Goodtech.JSON;
using SparkplugNet.VersionB.Data;

namespace Goodtech.Connectors
{
    /// <summary>
    /// An interface towards a MES system, etc.
    /// The interface has the necessary methods for simple operations to retrieve and send data to a target/source MES system,
    /// such as retrieving tags, updating tags, inserting new tags, and retrieving connection details.
    /// The implementing connector should have, or have methods for getting, the necessary details to retrieve these details.
    /// i.e: the TrakSys connector can get client configuration settings from the database,
    /// but doesn't have the connection details for the database on startup - it will therefore ask for these.
    /// </summary>
    public interface IConnector
    {
        /// <summary>
        /// Gets a list of all current tags, converted to <see cref="Metric"/> from the source.
        /// </summary>
        /// <returns>A List of tags as <see cref="Metric"/></returns>
        public List<Metric> GetTags();
        /// <summary>
        /// Gets a list of all current tags that have changed since last scan, converted to <see cref="Metric"/> from the source.
        /// </summary>
        /// <returns>A List of tags as <see cref="Metric"/></returns>
        public List<Metric> GetChangedTags();
        /// <summary>
        /// Gets a list of all current tags that have changed compared to the input list, converted to <see cref="Metric"/> from the source.
        /// </summary>
        /// <param name="existingTags">The dictionary that should be compared to.</param>
        /// <returns>A List of tags as <see cref="Metric"/></returns>
        public List<Metric> GetChangedTags(Dictionary<int, object> existingTags);
        /// <summary>
        /// Gets the configuration settings from the MES system, as a <see cref="ClientConfiguration"/> class.
        /// </summary>
        /// <returns>The configuration class to be used in initialization of the client.</returns>
        // /// <param name="sqlSettings">Contains the necessary information to connect to the database and retrieve the information.</param>
        public ClientConfiguration? GetClientConfiguration();
        /// <summary>
        /// Updates the tags in the target system. If using the TrakSys Connector, the tags will be updated one-by-one.
        /// The method should transform the metrics to the appropriate datatype or formatting for the target system.
        /// </summary>
        /// <param name="metrics">An enumerable list of the metrics that should be sent to the target.</param>
        public void UpdateTagsSingly(IEnumerable<Metric> metrics);
        /// <summary>
        /// Updates the tags in the target system simultaneously. 
        /// The method should transform the metrics to the appropriate datatype or formatting for the target system.
        /// </summary>
        /// <param name="metrics">An enumerable list of the metrics that should be sent to the target.</param>
        public void UpdateTagsConcurrently(IEnumerable<Metric> metrics);
        /// <summary>
        /// Inserts new tags to the target system. The tags should not already exist.
        /// If using the TrakSys Connector, the tags will be updated one-by-one.
        /// </summary>
        /// <param name="metrics">An enumerable list of the metrics that should be sent to the target.</param>
        public void InsertNewTags(IEnumerable<Metric> metrics);
        /// <summary>
        /// Transforms the object from the source system (connector) to the Sparkplug B <see cref="Metric"/> type.
        /// </summary>
        /// <param name="obj">The object that should have its properties transferred.</param>
        /// <returns>A Sparkplug B Metric.</returns>
        public Metric TransformSourceObjectToMetric(object obj);
        /// <summary>
        /// The connector will log the time spent on an operation, for the timers defined in the connector.
        /// </summary>
        public void LogResults();
    }
}