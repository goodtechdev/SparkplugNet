using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ETS.Core.Api;
using ETS.Core.Api.Models;
using ETS.Core.Api.Models.Data;
using ETS.Core.Extensions;
using Goodtech.Config;
using Goodtech.Connectors;
using Goodtech.JSON;
using Goodtech.Log;
using Goodtech.SparkplugB;
using Goodtech.Sql;
using Goodtech.Testing;
using Goodtech.Utils.Exceptions;
using Goodtech.Utils.Objects;
using Newtonsoft.Json;
using Serilog;
using SparkplugNet.VersionB.Data;
using DataType = ETS.Core.Enums.DataType;

namespace Goodtech.TrakSYS
{
    public class Connector : IConnector
    {
        private TimeCalc _apiInsertTimer = null!;
        private TimeCalc _apiUpdateTimer = null!;
        private TimeCalc _apiLoadTimer = null!;
        private TimeCalc _sqlUpdateTimer = null!;
        private static ClientConfiguration? _clientConfiguration;
        private static readonly string BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        private readonly string _pathApiInsertTimer = Path.Combine(BaseDirectory, "Logs/ApiInsertTimer.txt");
        private readonly string _pathApiUpdateTimer = Path.Combine(BaseDirectory, "Logs/ApiUpdateTimer.txt");
        private readonly string _pathSqlUpdateTimer = Path.Combine(BaseDirectory, "Logs/SqlUpdateTimer.txt");
        private readonly string _pathLoadTimer = Path.Combine(BaseDirectory, "Logs/ApiLoadTimer.txt");

        private static SqlSettings? _sqlConfig;

        /**
        * <summary>Working list of tags in the system. Tags are indexed by key: Tag.ID</summary>
        */
        private readonly Dictionary<int, DbTag> _tagsIndexed;

        protected ApiService Api;

        /**
        * <summary>Initial list of tags. Zero-based indexing.</summary>
        */
        private readonly IList? _tags;

        public Connector()
        {
            try
            {
                _sqlConfig = new SqlSettings(Constants.ApplicationIdentifier);
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message + "\nAre you connected to the TrakSYS Database?", "Error");
                throw new TimeoutException("Connection Timeout. Are you connected to the TrakSYS Database?");
            }

            try
            {
                var apiService = ConnectApi();
                Api = apiService;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message + "\nAre you connected to the TrakSYS Server?", "Error");
                throw new TimeoutException("API Connection Timeout. Are you connected to the TrakSYS Server?");
            }


            _clientConfiguration = GetClientConfiguration() ?? throw new TsConnectionException(
                "Unable to retrieve the connection configuration from the TrakSYS database. Check that the configuration exists in the database, and that the connection details are correct.");

            //TODO: Bytte metoden under til datatable?
            // var test = _tags;
            // test = SqlOperations.GetDataTableFromSql()

            //Get lists of tags from TrakSYS
            _tags = SqlOperations.GetObjectList("DbTag", Api) ??
                    throw new TsQueryException("Unable to retrieve tags from TrakSYS");
            _tagsIndexed = new Dictionary<int, DbTag>();


            //Index tags and events
            if (_tags != null)
                foreach (DbTag tag in _tags)
                {
                    _tagsIndexed.Add(tag.ID, tag);
                }


            if (!_clientConfiguration.SettingEnableTimeLogging) return;
            _apiInsertTimer = new TimeCalc();
            _apiUpdateTimer = new TimeCalc();
            _apiLoadTimer = new TimeCalc();
            _sqlUpdateTimer = new TimeCalc();
        }

        public void LogResults()
        {
            _apiInsertTimer.LogResults(_pathApiInsertTimer);
            _apiUpdateTimer.LogResults(_pathApiUpdateTimer);
            _apiLoadTimer.LogResults(_pathLoadTimer);
            _sqlUpdateTimer.LogResults(_pathSqlUpdateTimer);
        }


        /// <summary>
        /// Uses the <see cref="ListProducer"/> to retrieve a list of all tags in the TS database.
        /// Uses <see cref="TransformTagToMetric"/> to convert <see cref="DbTag"/> to <see cref="Metric"/>.
        /// </summary>
        /// <returns>A metric list of all tags in the database.</returns>
        public List<Metric> GetTags()
        {
            var metrics = new List<Metric>();

            // Retrieves a list of all tags in the TS database
            var tags = SqlOperations.GetObjectList("DbTag", Api);

            if (tags != null)
            {
                // Converts each DbTag in the tags list to a Metric, and adds it to the metrics list
                foreach (DbTag tag in tags)
                {
                    metrics.Add(TsFormatter.TransformTagToMetric(tag));
                }

                // Parses ISA-95 structure for each metric in the metrics list
                TsFormatter.ParseIsa95(metrics, _clientConfiguration!);
                return metrics;
            }

            return metrics;
        }

        /// <summary>
        /// Returns a list of Metric objects representing the tags that have changed since the last scan.
        /// </summary>
        /// <returns>A List of Metric objects representing the tags that have changed since the last scan.</returns>
        public List<Metric> GetChangedTags()
        {
            // Initialize a new list of Metric objects to store the changed tags.
            var metrics = new List<Metric>();

            // Get the latest tags from the TrakSYS database.
            var freshTagsZeroBased = SqlOperations.GetObjectList("DbTag", Api);
            if (freshTagsZeroBased is null) return metrics;

            // Initialize a new dictionary to store the fresh tags with the ID of the tag as the key.
            // Insert the fresh tags into the indexed list where the index is the ID of the tag.
            var freshIndexed = freshTagsZeroBased.Cast<DbTag>().ToDictionary(tag => tag.ID);

            // Compare the fresh tags against the old tags.
            foreach (var tag in freshIndexed.Values)
            {
                // If the tag is null, skip to the next iteration.
                if (tag == null) continue;

                // If the tag is not in the old tags, try to find it elsewhere.
                if (_tagsIndexed[tag.ID] == null)
                {
                    Logger.Information("Could not find element at ID: " + tag.ID + ". Will try to look elsewhere.");

                    // Look for the tag regardless of indexing.
                    foreach (var existing in _tagsIndexed.Values)
                    {
                        if (ObjectOperations.ComparePropertiesValues(existing, tag))
                        {
                            Logger.Error("Element found in Existing at " + existing.ID + ".");

                            // Element found. Look for change.
                            if (existing.Value != tag.Value)
                            {
                                metrics.Add(TsFormatter.TransformTagToMetric(tag));
                            }

                            break;
                        }
                    }
                }
                else if (_tagsIndexed.ContainsKey(tag.ID)) // Tag.ID exists from before.
                {
                    // If the tag value has changed, add it to the metrics payload.
                    if (_tagsIndexed[tag.ID].Value == tag.Value) continue;
                    _tagsIndexed[tag.ID] = tag;
                    metrics.Add(TsFormatter.TransformTagToMetric(tag));
                }
            }

            // Parse the metrics with ISA-95 information from the client configuration.
            TsFormatter.ParseIsa95(metrics, _clientConfiguration!);

            // Return the list of changed tags as a list of Metric objects.
            return metrics;
        }


        /// <summary>
        /// Returns a list of changed tags since the last scan, compared to the given existing tags.
        /// </summary>
        /// <param name="existingTags">A dictionary containing the existing tags.</param>
        /// <returns>A list of changed metrics.</returns>
        public List<Metric> GetChangedTags(Dictionary<int, object> existingTags)
        {
            // Cast the dictionary values to DbTag objects and create a new dictionary with ID as the key
            var tagDict = existingTags.ToDictionary(kvp => kvp.Key, kvp => (DbTag)kvp.Value);

            var metrics = new List<Metric>();
            var freshTagsZeroBased = SqlOperations.GetObjectList("DbTag", Api);
            var freshIndexed = freshTagsZeroBased.Cast<DbTag>().ToDictionary(tag => tag.ID);

            // Insert fresh tags into an indexed list where the index is the ID of the tag

            // Check fresh tags up against old tags
            foreach (var tag in freshIndexed.Values)
            {
                if (tag is null) continue;
                if (tagDict[tag.ID] == null)
                {
                    Logger.Debug("Could not find element at ID: " + tag.ID + ". Will try to look elsewhere.");
                    foreach (var existing in tagDict.Values)
                    {
                        // Look for object regardless of indexing
                        if (ObjectOperations.ComparePropertiesValues(existing, tag))
                        {
                            Logger.Debug("Element found in Existing at " + existing.ID + ".");
                            // Element found. Look for change.
                            if (existing.Value != tag.Value)
                            {
                                metrics.Add(TsFormatter.TransformTagToMetric(tag));
                            }

                            break;
                        }
                    }
                }
                else if (tagDict.ContainsKey(tag.ID))
                {
                    // Tag.ID exists from before
                    if (tagDict[tag.ID].Value != tag.Value)
                    {
                        // There has been a change in value
                        metrics.Add(TsFormatter.TransformTagToMetric(tag));
                    }
                    else
                    {
                        continue;
                    }
                }
            }

            // Convert ISA95 metrics and return the list
            TsFormatter.ParseIsa95(metrics, _clientConfiguration!);
            return metrics.Count > 0 ? metrics : null;
        }

        /// <summary>
        ///     Uses the TS API to update tags in the database. The tags must exist from before.
        /// </summary>
        /// <param name="tags"></param>
        private void UpdateExistingTags(List<DbTag> tags)
        {
            foreach (var tag in tags)
            {
                var sample = new Sample()
                {
                    name = tag.Name,
                    rx = DateTime.UtcNow.Millisecond
                };
                try
                {
                    Api.Data.DbTag.Save.UpdateExisting(tag);
                    Logger.Information("Updating " + tag.Name + " with new value: " + tag.Value);
                }
                catch (Exception e)
                {
                    Logger.Error(e.Message);
                    throw;
                }

                sample.tx = DateTime.UtcNow.Millisecond;
                _apiUpdateTimer.Samples.Add(sample);
            }
        }

        /// <summary>
        /// Uses the TS API to save new tags to the TrakSYS database.
        /// </summary>
        /// <param name="tags">The list of DbTagVirtualComposite objects to save.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        private Task PushNewTags(List<DbTagVirtualComposite> tags)
        {
            // Set the tag group ID to 1 (FromConsumer/MQTT.Imported in TrakSYS).
            const int tagGroup = 1;

            // For each DbTagVirtualComposite in the list of tags:
            foreach (var tag in tags)
            {
                // Create a new sample object to represent the current time and tag name.
                var sample = new Sample()
                {
                    name = tag.Name,
                    rx = DateTime.UtcNow.Millisecond
                };

                try
                {
                    // Set the TagGroupID property of the tag to the tagGroup ID.
                    tag.TagGroupID = tagGroup;

                    // Validate the new tag using the TS API.
                    var validate = Api.Data.DbTagVirtualComposite.Validate.AsNew(tag).Return;
                    if (validate)
                    {
                        var result = Api.Data.DbTagVirtualComposite.Save.InsertAsNew(tag, ignoreWarnings: false)
                            .ThrowIfFailed();
                        Logger.Information("Pushing " + tag.Name + " to TrakSYS: ");
                    }

                    // Save the new tag to the TrakSYS database using the TS API.
           
                }
                catch (Exception e)
                {
                    Logger.Error(e.Message);
                    //throw;
                }

                // Add the current time to the _apiInsertTimer's samples list for testing purposes.
                sample.tx = DateTime.UtcNow.Millisecond;
                _apiInsertTimer.Samples.Add(sample);
            }

            // Return a completed Task.
            return Task.CompletedTask;
        }


        private DbTagVirtualComposite TransformTagToVirtual(DbTag tag)
        {
            var tagVirtual = new DbTagVirtualComposite();
            var tagVirtualProps = tagVirtual.GetType().GetProperties();

            foreach (var prop in tag.GetType().GetProperties())
            {
                if (prop.Name != "Guid")
                {
                    try
                    {
                        var virtualProp = tagVirtual.GetType().GetProperty(prop.Name);
                        virtualProp?.SetValue(tag, prop.Name);
                    }
                    catch (Exception e)
                    {
                        Logger.Debug("Virtual tag doesn't have property: " + prop.Name + ".");
                    }
                }
            }

            return tagVirtual;
        }

        /// <summary>
        ///     Saves new metrics as new tags in TrakSYS.
        /// </summary>
        /// <param name="metrics">The metrics that should be implemented.</param>
        public void InsertNewTags(IEnumerable<Metric> metrics)
        {
            // var transformedTags = new List<DbTag>();
            // foreach (var metric in metrics.Values) transformedTags.Add(MetricHandler.TransformMetricToTag(metric));
            var transformedTags = new List<DbTagVirtualComposite>();
            foreach (var metric in metrics)
            {
                transformedTags.Add(TsFormatter.TransformMetricToTagVirtual(metric));
            }

            PushNewTags(transformedTags);
        }

        /// <summary>
        /// Updates the values of existing tags in TrakSYS using a single API update statement for each tag.
        /// </summary>
        /// <param name="metrics">The metrics to update as tags.</param>
        public void UpdateTagsSingly(IEnumerable<Metric> metrics)
        {
            // Initialize a new list to hold the transformed tags.
            var transformedTags = new List<DbTag>();

            // Transform each metric into a DbTag and add it to the transformedTags list.
            foreach (var metric in metrics)
            {
                // Create a new sample object to represent the current time and metric name.
                var sample = new Sample()
                {
                    name = metric.Name,
                    rx = DateTime.UtcNow.Millisecond
                };

                // Transform the metric to a DbTag object.
                var tag = TsFormatter.TransformMetricToTag(metric);

                // Load the tag from TrakSYS or throw an exception if it doesn't exist.
                var tagTs = Api.Data.DbTag.Load.ByName(tag.Name)
                            ?? throw new InvalidDataException("Couldn't update tag: " + tag.Name +
                                                              ". Does the tag exist in TrakSYS?");

                // Set the value and update time of the tag to the current values.
                tagTs.Value = tag.Value;
                tagTs.UpdateDateTime = tag.UpdateDateTime;

                // Add the transformed tag to the transformedTags list.
                transformedTags.Add(tagTs);

                // Add the current time to the _apiLoadTimer's samples list for testing purposes.
                sample.tx = DateTime.UtcNow.Millisecond;
                _apiLoadTimer.Samples.Add(sample);
            }

            // Update the values of the existing tags in TrakSYS using a single SQL update statement for each tag.
            UpdateExistingTags(transformedTags);
        }


        /// <summary>
        /// Updates TrakSYS with new values for existing metrics.
        /// </summary>
        /// <param name="metrics">The metrics that already exist.</param>
        public void UpdateTagsConcurrently(IEnumerable<Metric> metrics)
        {
            // Initialize a new list to hold the transformed tags.
            var transformedTags = new List<DbTag>();

            // Transform each metric into a DbTag and add it to the transformedTags list.
            foreach (var metric in metrics)
            {
                // Create a new sample object to represent the current time and metric name.
                var sample = new Sample()
                {
                    name = metric.Name,
                    rx = DateTime.UtcNow.Millisecond
                };

                // Transform the metric to a DbTag object.
                var tag = TsFormatter.TransformMetricToTag(metric);

                // Set the value and update time of the tag to the current values.
                tag.Value = tag.Value;
                tag.UpdateDateTime = tag.UpdateDateTime;

                // Add the transformed tag to the transformedTags list.
                transformedTags.Add(tag);
            }

            // Build the SQL update statement string to update the tag values in the database.
            var sql = $"UPDATE {_sqlConfig!.Name}.[dbo].[tTag] SET Value = CASE\n";

            // Initialize a new list to hold the names of the tags being updated.
            var tagNames = new List<string>();

            // Build the CASE statement for each tag being updated.
            foreach (var tag in transformedTags)
            {
                // If time logging is enabled
                if (_clientConfiguration!.SettingEnableTimeLogging)
                {
                    // Create a new sample object to represent the current time and tag name.
                    var sample = new Sample()
                    {
                        name = tag.Name,
                        rx = DateTime.UtcNow.Millisecond
                    };
                    // Add the sample to the _sqlUpdateTimer's samples list.
                    _sqlUpdateTimer.Samples.Add(sample);
                }

                // Add the tag name to the tagNames list.
                tagNames.Add("'" + tag.Name + "'");

                // Add the CASE statement for the current tag to the SQL update statement string.
                sql += $"WHEN [Name] = '{tag.Name}' THEN '{tag.Value}'\n";
            }

            // Join the tagNames list into a string with commas and add it to the SQL update statement string.
            var tagNamesStr = string.Join(",", tagNames) + "\n";
            sql += $"END\nWHERE [Name] IN ({tagNamesStr});\n";

            // Log the number of transformed tags being written to the database.
            Logger.Information("Writing " + transformedTags.Count + " values to database.");

            // Write the SQL update statement to the database.
            SqlOperations.WriteWithSql(sql, _sqlConfig!);

            // Update the samples in the _sqlUpdateTimer's samples list with the current time and average difference.
            foreach (var tag in transformedTags)
            {
                var sample = _sqlUpdateTimer.Samples.Last(s => s.name == tag.Name);
                sample.tx = DateTime.UtcNow.Millisecond;
                sample.diff /= transformedTags.Count;
            }
        }

        /// <summary>
        /// Attempts to write s SQL string to a database specified in SqlSettings.
        /// </summary>
        /// <param name="sql">The SQL string.</param>
        /// <param name="sqlSettings">The <see cref="SqlSettings"/> that specifies connection details.</param>
        private static void WriteWithSql(string sql, SqlSettings sqlSettings)
        {
            try
            {
                // Open a connection to the database
                using (SqlConnection connection = new SqlConnection(sqlSettings.GetConnectionString()))
                {
                    connection.Open();

                    // Create a command object with the SQL query and the connection
                    using (SqlCommand command = new SqlCommand(sql, connection))
                    {
                        // Execute the command and get the number of affected rows
                        int rowsAffected = command.ExecuteNonQuery();
                    }
                }
            }
            catch (SqlException ex)
            {
                // Handle any SQL exceptions
                Console.WriteLine("SQL error: " + ex.Message);
            }
            catch (Exception ex)
            {
                // Handle any other exceptions
                Console.WriteLine("Error: " + ex.Message);
            }
        }


        /// <summary>
        /// Gets the client configuration object for the primary client.
        /// </summary>
        /// <returns>The <see cref="ClientConfiguration"/> object.</returns>
        public ClientConfiguration? GetClientConfiguration()
        {
            // Build the SQL query string to select the primary client configuration from the database.
            var sql =
                $@"SELECT * FROM [{_sqlConfig!.Name}].[dbo].[{Constants.ClientConfigurationTable}] WHERE [Name] LIKE 'Primary'";

            // Get the client configuration data from the database and store it in a datatable.
            var datatable = SqlOperations.GetDataTableFromSql(_sqlConfig.GetConnectionString(), sql);

            // Create a new ClientConfiguration object to hold the converted data from the datatable.
            var config = new ClientConfiguration();

            try
            {
                // Convert the data in the datatable to the ClientConfiguration object using the TsFormatter.ConvertDatatableToObject method.
                config = SqlOperations.ConvertDatatableToObject<ClientConfiguration>(datatable, config); //TODO: Test
            }
            catch (Exception ex)
            {
                // If there was an exception during the conversion, log the error message.
                Logger.Error(ex.Message);
                return null;
            }

            // Return the ClientConfiguration object.
            return config;
        }


        #region transform

        /// <summary>
        /// Transforms an object of the system type (here TrakSYS <see cref="DbTag"/> to a <see cref="Metric"/> 
        /// </summary>
        /// <param name="obj">The object that should be transformed.</param>
        /// <returns>A metric with the relevant values.</returns>
        public Metric TransformSourceObjectToMetric(object obj)
        {
            var tag = TsFormatter.TransformObjectToTag(obj);
            var metric = TsFormatter.TransformTagToMetric(tag);
            return metric;
        }

        #endregion

        /**
        * <summary>Connects to the TrakSYS API. Uses a .json file for login and connection details.</summary>
        */
        private static ApiService ConnectApi()
        {
            var connectionInfo = new ConnectionInfo();
            var apiLoginSettings = new TsApiLogin();

            //Get connection details from client configuration, and insert them to the LoginApi class.
            foreach (var prop in apiLoginSettings.GetType()
                         .GetProperties()) //Using reflection to append settings to connectionInfo
            {
                //Get the property from client configuration
                var originalProp = _sqlConfig?.GetType().GetProperty(prop.Name);
                //Get the value of the property
                var value = originalProp?.GetValue(_sqlConfig, null);
                //Set the value of the new prop
                prop.SetValue(apiLoginSettings, value);
            }

            foreach (var setting in
                     _sqlConfig!.GetType().GetProperties()) //Using reflection to append settings to connectionInfo
            {
                //Get the property in connectionInfo 
                var property = connectionInfo.GetType().GetProperty(setting.Name);
                var value = setting.GetValue(_sqlConfig, null);
                property?.SetValue(connectionInfo, value);
            }

            try
            {
                var api = ApiService
                    .StartupNewApplicationWithConnectionSettings(connectionInfo); //Try to connect to the service.
                return api;
            }
            catch (Exception e)
            {
                Logger.Error(e.Message);
                throw new TimeoutException(e.Message);
            }
        }
    }
}