using System;

namespace Goodtech.Config
{
    // This class represents the configuration settings for a Sparkplug B MQTT client
    public class ClientConfiguration
    {

        // The name of the configuration
        public string Name { get; set; }

        // The hostname of the MQTT broker to connect to
        public string MqttHostname { get; set; }

        // The port number of the MQTT broker to connect to
        public int MqttPort { get; set; }

        // Whether or not to use TLS encryption for the MQTT connection
        public bool MqttUseTls { get; set; }

        // The username to use when connecting to the MQTT broker
        public string MqttUsername { get; set; }

        // The password to use when connecting to the MQTT broker
        public string MqttPassword { get; set; }

        // The MQTT client ID to use when connecting to the broker
        public string MqttClientId { get; set; }

        // The Sparkplug B namespace to use for MQTT topics
        public string MqttSpBNamespace { get; set; }

        // The group ID for this client (if any)
        public string MqttGroupId { get; set; }

        // The node ID for this client (if any)
        public string MqttNodeId { get; set; }

        // The device ID for this client (if any)
        public string MqttDeviceId { get; set; }
        
        // The timout timespan in seconds
        public int MqttTimeout { get; set; } = 30;

        // Whether or not to automatically subscribe to group-level topics
        public string LookupSetGroupSubscriptions { get; set; }

        // Whether or not to automatically subscribe to node and device-level topics
        public string LookupSetNodesDevicesSubscriptions { get; set; }

        // Whether or not to enable discovery of other Sparkplug B nodes on the network
        public bool SettingEnableDiscovery { get; set; }

        // Whether or not to enable pushing data to an SQL database
        public bool SettingEnableSqlPush { get; set; }

        // Whether or not to enable time logging of messages received from the MQTT broker
        public bool SettingEnableTimeLogging { get; set; }

        // The name of the SQL database to push data to (if applicable)
        public string DbName { get; set; }

        // The hostname of the SQL database server (if applicable)
        public string DbHostname { get; set; }

        // The username to use when connecting to the SQL database (if applicable)
        public string DbUsername { get; set; }

        // The password to use when connecting to the SQL database (if applicable)
        public string DbPassword { get; set; }

        // Constructor that initializes all properties to their default values
        public ClientConfiguration()
        {
            Name = string.Empty;
            MqttHostname = string.Empty;
            MqttPort = 0;
            MqttUseTls = false;
            MqttUsername = string.Empty;
            MqttPassword = string.Empty;
            MqttClientId = string.Empty;
            MqttSpBNamespace = string.Empty;
            MqttGroupId = string.Empty;
            MqttNodeId = string.Empty;
            MqttDeviceId = string.Empty;
            MqttTimeout = 30;
            LookupSetGroupSubscriptions = string.Empty;
            LookupSetNodesDevicesSubscriptions = string.Empty;
            SettingEnableDiscovery = false;
            SettingEnableSqlPush = false;
            SettingEnableTimeLogging = false;
            DbName = string.Empty;
            DbHostname = string.Empty;
            DbUsername = string.Empty;
            DbPassword = string.Empty;
        }
    }
}