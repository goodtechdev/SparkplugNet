using System;
using System.Data;
using Goodtech.Log;

namespace Goodtech.Sql
{
    /// <summary>
    /// A class that handles the settings required to connect to a SQL Server database.
    /// </summary>
    public class SqlSettings
    {
        /// <summary>
        /// The name of the database.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// The hostname of the SQL Server instance.
        /// </summary>
        public string? Server { get; set; }

        /// <summary>
        /// The username to connect to the database.
        /// </summary>
        public string? Login { get; set; }

        /// <summary>
        /// The password to connect to the database.
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// Indicates whether to use Windows Authentication to connect to the database.
        /// </summary>
        public bool UsesWindowsAuthentication { get; set; } = false;

        /// <summary>
        /// An identifier for the program/client. Used in the database logging.
        /// </summary>
        public string ApplicationName { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlSettings"/> class with the specified identifier.
        /// </summary>
        /// <param name="identifier">The identifier for the program/client.</param>
        public SqlSettings(string identifier)
        {
            // Initialize the ApplicationName property with the identifier.
            ApplicationName = identifier;

            // Try to get the connection settings from environment variables.
            if (TryGetConnectionSettings()) return;

            // If the environment variables are not set or are incomplete, prompt the user to enter the missing details manually.
            Logger.Information("Necessary connection details not found. Please enter manually:");
            Console.WriteLine("Please enter the necessary details manually:");
            foreach (var prop in this.GetType().GetProperties())
            {
                // Skip the UsesWindowsAuthentication property because it is not a required setting.
                if(prop.Name == nameof(UsesWindowsAuthentication)) continue;

                // Prompt the user to enter the property value.
                Console.Write(prop.Name + ": ");
                var value = Console.ReadLine();

                // Set the property value with the user input.
                prop.SetValue(this, value);

                // Store the property value as an environment variable.
                Console.Write(" -> OK.");
                try
                {
                    var envName = ApplicationName + "_" + prop.Name;
                    Environment.SetEnvironmentVariable(envName, value, EnvironmentVariableTarget.Machine);
                    Console.WriteLine("Storing " + prop.Name + " as environment variable under: " + envName);
                    Logger.Information("Storing " + prop.Name + " as environment variable under: " + envName);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.Message);
                    throw;
                }
            }
        }

        /// <summary>
        /// Tries to get the connection settings from environment variables.
        /// </summary>
        /// <returns><c>true</c> if all required environment variables are found; otherwise, <c>false</c>.</returns>
        private bool TryGetConnectionSettings()
        {
            try
            {
                // Get the values of the environment variables for the connection settings.
                Server = Environment.GetEnvironmentVariable(ApplicationName+"_Server", EnvironmentVariableTarget.Machine) ?? throw new DataException("Couldn't find the Environment Variable for the Database DbHostname.");
                Name = Environment.GetEnvironmentVariable(ApplicationName+"_Name", EnvironmentVariableTarget.Machine);
                Login = Environment.GetEnvironmentVariable(ApplicationName+"_Login", EnvironmentVariableTarget.Machine);
                Password = Environment.GetEnvironmentVariable(ApplicationName+"_Password", EnvironmentVariableTarget.Machine);

                // If all required environment variables are found, return true.
                return Server != null && Login != null && Password != null;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Gets the connection string for the SQL Server database.
        /// </summary>
        /// <returns>The connection string.</returns>
        public string GetConnectionString()
        {
            // Concatenate the connection string components into a string in the required format for the SqlConnection object.
            return $"User ID={this.Login};Password={this.Password};Server={this.Server}";
        }
    }
}
