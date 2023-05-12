namespace Goodtech.JSON
{
    /// <summary>
    /// A simple class for keeping the properties needed to connect to the TrakSYS database
    /// </summary>
    public class TsApiLogin
    {
        public string ApplicationName { get; set; } = "MQTT_Producer"; // Her kan det stå hva som helst.
        public string Server { get; set; } // Server der databasen ligger IP-addrese.
        public string Name { get; set; } // Navn på Database.
        public bool UsesWindowsAuthentication { get; set; } = false;
        public string Login { get; set; } // Login Database.
        public string Password { get; set; } // Passord Login. 
    }
}