using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using ETS.Core.Api;
using Goodtech.Log;
using Goodtech.Utils.Exceptions;

namespace Goodtech.Sql
{
    /// <summary>
    /// A class for handling SQL Operations.
    /// </summary>
    public static class SqlOperations
    {
        /// <summary>
        /// Attempts to write s SQL string to a database specified in SqlSettings.
        /// </summary>
        /// <param name="sql">The SQL string.</param>
        /// <param name="sqlSettings">The <see cref="SqlSettings"/> that specifies connection details.</param>
        public static void WriteWithSql(string sql, SqlSettings sqlSettings)
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
                Logger.Error("SQL error: " + ex.Message);
            }
            catch (Exception ex)
            {
                // Handle any other exceptions
                Logger.Error("Error: " + ex.Message);
            }
        }
        /// <summary>
        /// The method uses a typical SQL connection and SQL query to retrieve a datatable from a database.
        /// </summary>
        /// <param name="connectionString">The SQL connection string.</param>
        /// <param name="query">The SQL Select query.</param>
        /// <returns></returns>
        /// <exception cref="SqlException"></exception>
        /// <exception cref="Exception"></exception>
        public static DataTable GetDataTableFromSql(string connectionString, string query)
        {
            try
            {
                // Create a new SqlConnection object with the provided connection string
                using var connection = new SqlConnection(connectionString);
                // Create a new SqlDataAdapter object with the provided query and SqlConnection object
                using var adapter = new SqlDataAdapter(query, connection);
                // Create a new DataTable object to hold the results of the query
                var dataTable = new DataTable();

                // Use the SqlDataAdapter object to fill the DataTable object with data from the database
                adapter.Fill(dataTable);

                // Return the populated DataTable object
                return dataTable;
            }
            catch (SqlException ex)
            {
                // Log the error message
                Logger.Error(ex + "\n" + connectionString + "\n" + query);
                // Rethrow the exception or return an empty DataTable
                throw ex;
                // return new DataTable();
            }
            catch (Exception ex)
            {
                // Log the error message
                Logger.Error(ex + "\n" + connectionString + "\n" + query);

                // Rethrow the exception or return an empty DataTable
                throw ex;
                // return new DataTable();
            }
        }
        public static T ConvertDatatableToObject<T>(DataTable dataTable, object obj)
        {
            //Store the columns
            string[] keys = dataTable.Columns.Cast<DataColumn>()
                .Select(column => column.Caption)
                .ToArray();
            //Store the values
            var values = dataTable.Rows[0].ItemArray;

            //Store in dictionary
            Dictionary<string, object> dataDict = keys.Zip(values, (k, v) => new { Key = k, Value = v })
                .ToDictionary(x => x.Key, x => x.Value);

            //Where keys = obj.prop, write the value
            foreach (PropertyInfo prop in obj.GetType().GetProperties())
            {
                try
                {
                    string name = prop.Name;              //The name of the property
                    var value = dataDict[name];
                    var propValue = prop.GetValue(obj);

                    //Check if value is DBNull
                    if (value == DBNull.Value)
                    {
                        //Set the property value to null
                        prop.SetValue(obj, null);
                    }
                    else if (prop.PropertyType == typeof(bool) && value is string)
                    {
                        // Convert "0" and "1" strings to boolean values
                        bool boolValue = (value == "1");
                        prop.SetValue(obj, boolValue);
                    }
                    else
                    {
                        //Cast value
                        var castValue = Convert.ChangeType(value, propValue.GetType());
                        prop.SetValue(obj, castValue);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.Message);
                }
            }
            return (T)obj;
        }
        /// <summary>Returns a list of all rows in a table in the TrakSYS database. Uses the TS API.</summary>
        /// <param name="dbType">The string name of the type. i.e: "DbTag", "DbEvent"</param>
        /// <param name="api"></param>
        public static IList? GetObjectList(string dbType, ApiService api)
        {
            //Gets objects of (dbType) from (tType) from the TrakSys instance.
            try
            {
                var entity = api.Data.GetType().GetProperty(dbType);
                var entityDb = entity?.GetValue(api.Data, null);
                var entityList = entityDb?.GetType().GetProperty("GetList")?.GetValue(entityDb);
                var method = entityList?.GetType().GetMethod("WithSql");
                var tType = "t" + dbType.Substring(2);
                var sql = @"SELECT * FROM " + tType;
                var list = (IList)method?.Invoke(entityList, new object[] { sql });

                return list;
            }
            catch (Exception ex)
            {
                Logger.Information(ex.Message);
                throw new TsQueryException("Unable to retrieve a list:\n" + ex.Message);
            }

            return null;
        }
    }
    
}