using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Goodtech.Config;
using Goodtech.Connectors;
using Goodtech.JSON;
using Goodtech.SparkplugB;
using Serilog;
using SparkplugNet.Core;
using SparkplugNet.Core.Application;
using SparkplugNet.Core.Enumerations;
using SparkplugNet.Core.Node;
using SparkplugNet.VersionB;
using SparkplugNet.VersionB.Data;



public class Producer
{
    public static IConnector Connector;
    private static ClientConfiguration _clientConfiguration;
    public static bool Restarting { get; private set; }
    private static StoredStates _nodeConfig;
    private static List<Metric> _usingMetrics;
    private const string PathKnownMetrics = @"../../knownMetrics.json";
    public static int ScanRate = 50;

#pragma warning restore CS8604 // Possible null reference argument.
    private static SparkplugNodeOptions _nodeOptions;
    private static SparkplugNode _node;
#pragma warning restore CS8604 // Possible null reference argument.

    /// <summary>
    /// The cancellation token source.
    /// </summary>
    private static readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();

    /// <summary>
    /// Initializes the classes needed to run the node.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NullReferenceException">Throws an exception if some of the necessary classes weren't initialized.</exception>
    public static Task Init()
    {

        //Generate a configuration for this 
        _nodeConfig = ConfigHandler.GenerateAppConfiguration(Connector.GetTags(), PathKnownMetrics);

        //Get the client configuration from the connector
        //For the example: consider creating an empty object of class ClientConfiguration, and enter the properties manuall
        //or enter them into the next constructor manually.
        _clientConfiguration = Connector.GetClientConfiguration()
                               ?? throw new NullReferenceException(
                                   "Unable to retrieve the ClientConfiguration for the application.");
        //Construct the node options based on the configuration read.
        _nodeOptions = new SparkplugNodeOptions(
            _clientConfiguration.MqttHostname, 
            _clientConfiguration.MqttPort, 
            _clientConfiguration.MqttClientId + "_Producer",
            _clientConfiguration.MqttUsername, 
            _clientConfiguration.MqttPassword, 
            _clientConfiguration.MqttUseTls, 
            _clientConfiguration.MqttClientId + "_Producer",
            TimeSpan.FromSeconds(_clientConfiguration.MqttTimeout), 
            SparkplugMqttProtocolVersion.V311, 
            () => null, 
            null, 
            null, 
            _clientConfiguration.MqttGroupId, 
            _clientConfiguration.MqttNodeId, 
            CancellationTokenSource.Token);
        //Construct the node
        _node = new SparkplugNode(_usingMetrics = _nodeConfig.KnownMetrics.Values.ToList(), Log.Logger);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Runs the version B application.
    /// </summary>
    /// <returns>A <see cref="Task" /> representing any asynchronous operation.</returns>
    public static async Task Run()
    {
        // Start an application.
        Log.Information("Starting node...");
        await _node.Start(_nodeOptions);
        Log.Information("Node started...");

        //Publish a device birth
        var deviceMetrics = Connector.GetTags();
        await _node.PublishDeviceBirthMessage(deviceMetrics, _clientConfiguration.MqttDeviceId);

        #region EventHandlers

        // Get the known devices.
        var knownDevices = _node.KnownDevices;
        // Get the known node metrics from a node.
        var currentlyKnownMetrics = _node.KnownMetrics;
        // Check whether a node is connected.
        var isApplicationConnected = _node.IsConnected;
        // Handles the node's connected and disconnected events.
        _node.ConnectedAsync += OnVersionBNodeConnected;
        _node.DisconnectedAsync += OnVersionBNodeDisconnected;
        // Handles the node's device related events.
        _node.DeviceBirthPublishingAsync += OnVersionBNodeDeviceBirthPublishing;
        _node.DeviceCommandReceivedAsync += OnVersionBNodeDeviceCommandReceived;
        _node.DeviceDeathPublishingAsync += OnVersionBNodeDeviceDeathPublishing;
        // Handles the node's node command received event.
        _node.NodeCommandReceivedAsync += OnVersionBNodeNodeCommandReceived;
        // Handles the node's status message received event.
        _node.StatusMessageReceivedAsync += OnVersionBNodeStatusMessageReceived;

        #endregion
    }

    /// <summary>
    /// Stops the node, and saves the configuration.
    /// </summary>
    public static async Task Stop()
    {
        await _node.Stop();
        // Save configuration to local file
        await ConfigHandler.SaveConfiguration(_nodeConfig, PathKnownMetrics);
    }

    /// <summary>
    /// The running program. Updates values and publishes messages.
    /// </summary>
    public static async Task Update()
    {
        //Get the changed tags from the connector.
        _usingMetrics = Connector.GetChangedTags();

        //Publish data if there are changed tags 
        if (_usingMetrics.Count > 0)
        {
            await _node.PublishDeviceData(_usingMetrics, _clientConfiguration.MqttDeviceId); //DDATA
            //await _node.PublishMetrics(_usingMetrics);                                        //NDATA
        }

        
    }

    #region functions

    /// <summary>
    /// Handles a command from any node/device on Sparkplug B.
    /// </summary>
    /// <param name="args">The Sparkplug B arguments. i.e the metrics and metadata.</param>
    private static async Task HandleCommand(SparkplugNodeBase<Metric>.NodeCommandEventArgs args)
    {
        if (args.EdgeNodeIdentifier == _clientConfiguration.MqttNodeId)
        {
            Log.Information("Command received: " + args.Metric.Name);
            switch (args.Metric.Name)
            {
                case "Node Control/Rebirth":
                    await _node.PublishNodeBirthMessage();
                    break;
                case "Node Control/Scan rate":
                    ScanRate = (int)args.Metric.IntValue;
                    break;
                case "Device Control/Rebirth":
                    _usingMetrics = Connector.GetTags();
                    await _node.PublishDeviceBirthMessage(_usingMetrics, _clientConfiguration.MqttDeviceId);
                    break;
                default:
                    Log.Information("Unknown command received: " + args.Metric.Name);
                    break;
            }
        }
    }

    #endregion

    #region VersionBEvents

    /// <summary>
    ///     Handles the connected callback for version B applications.
    /// </summary>
    private static Task OnApplicationVersionBConnected(SparkplugBase<Metric>.SparkplugEventArgs arg)
    {
        // Do something.
        Console.WriteLine("Connected.");
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Handles the disconnected callback for version B applications.
    /// </summary>
    private static Task OnApplicationVersionBDisconnected(SparkplugBase<Metric>.SparkplugEventArgs arg)
    {
        // Do something.
        Console.WriteLine("Disconnected");
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Handles the device birth received callback for version B applications.
    /// </summary>
    private static Task OnApplicationVersionBDeviceBirthReceived(
        SparkplugBase<Metric>.DeviceBirthEventArgs arg)
    {
        // Do something.
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Handles the device data callback for version B applications.
    /// </summary>
    private static Task OnApplicationVersionBDeviceDataReceived(
        SparkplugApplicationBase<Metric>.DeviceDataEventArgs args)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Handles the device death received callback for version B applications.
    /// </summary>
    private static Task OnApplicationVersionBDeviceDeathReceived(
        SparkplugBase<Metric>.DeviceEventArgs arg)
    {
        // Do something.
        Console.WriteLine("Device death received.");
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Handles the node birth received callback for version B applications.
    /// </summary>
    private static Task OnApplicationVersionBNodeBirthReceived(
        SparkplugBase<Metric>.NodeBirthEventArgs arg)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Handles the node data callback for version B applications.
    /// </summary>
    private static Task OnApplicationVersionBNodeDataReceived(
        SparkplugApplicationBase<Metric>.NodeDataEventArgs args)
    {
        // Do something.
        Console.WriteLine("Data received.");
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Handles the node death received callback for version B applications.
    /// </summary>
    private static Task OnApplicationVersionBNodeDeathReceived(SparkplugBase<Metric>.NodeEventArgs arg)
    {
        // Do something.
        Console.WriteLine("Node death received.");
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Handles the connected callback for version B nodes.
    /// </summary>
    private static Task OnVersionBNodeConnected(SparkplugBase<Metric>.SparkplugEventArgs arg)
    {
        // Do something.
        Console.WriteLine("Connected.");
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Handles the disconnected callback for version B nodes.
    /// </summary>
    private static Task OnVersionBNodeDisconnected(SparkplugBase<Metric>.SparkplugEventArgs arg)
    {
        // Do something.
        Console.WriteLine("Disconnected.");
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Handles the device birth callback for version B nodes.
    /// </summary>
    /// <param name="args">The received args.</param>
    private static Task OnVersionBNodeDeviceBirthPublishing(
        SparkplugBase<Metric>.DeviceBirthEventArgs args)
    {
        // Do something.


        return Task.CompletedTask;
    }

    /// <summary>
    ///     Handles the device command callback for version B nodes.
    /// </summary>
    /// <param name="args">The received args.</param>
    private static Task OnVersionBNodeDeviceCommandReceived(
        SparkplugNodeBase<Metric>.NodeCommandEventArgs args)
    {
        HandleCommand(args).Wait();
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Handles the device death callback for version B nodes.
    /// </summary>
    /// <param name="args">The received args.</param>
    private static Task OnVersionBNodeDeviceDeathPublishing(SparkplugBase<Metric>.DeviceEventArgs args)
    {
        // Do something.
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Handles the node command callback for version B nodes.
    /// </summary>
    /// <param name="args">The received args.</param>
    private static Task OnVersionBNodeNodeCommandReceived(
        SparkplugNodeBase<Metric>.NodeCommandEventArgs args)
    {
        if (args.Metric.Name != "bdSeq")
        {
            Log.Information("Command received: " + args.Metric.Name);
            HandleCommand(args).Wait();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Handles the status message callback for version B nodes.
    /// </summary>
    /// <param name="args">The args.</param>
    private static Task OnVersionBNodeStatusMessageReceived(
        SparkplugNodeBase<Metric>.StatusMessageEventArgs args)
    {
        // Do something.
        return Task.CompletedTask;
    }

    #endregion
}

