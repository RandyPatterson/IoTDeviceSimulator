using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading.Tasks;

namespace IoTDeviceSimulator
{
    internal class Program
    {
        private static DeviceClient deviceClient;
        private const string deviceConnString = "HostName=MyDevices.azure-devices.net;DeviceId=DevSim01;SharedAccessKey=r1Wq1EA2jvUvbiUPvkQoOv2lNNmTvhHXSAdYY5WnYqY=";
        private const string deviceId = "DevSim01";
        private static readonly TwinCollection twinProperties = new TwinCollection();
        private static volatile int freq = 1000;
        private static volatile bool _sendTelemetry = true;

        public static void Main(string[] args)
        {
            ConsoleWrite("Simulated device. Press any key to exit.\n");
            deviceClient = DeviceClient.CreateFromConnectionString(deviceConnString, TransportType.Mqtt);
            deviceClient.ProductInfo = "IoT Workshop Simulated Device";

            //Set callback method when Desired Twin property changes
            deviceClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChanged, null).Wait();

            //Start sending Telemetry as a background thread
            SendDeviceToCloudMessagesAsync();

            //Start receiving Cloud to Device messages as a background thread
            ReceiveCloudToDeviceAsync();

            //Setup Direct Methods
            deviceClient.SetMethodHandlerAsync("start", StartTelemetry, null);
            deviceClient.SetMethodHandlerAsync("stop", StopTelemetry, null);

            Console.ReadKey();
        }

        /// <summary>
        ///     Background task for sending telemetry using frequency from Reported property 'freq'
        /// </summary>
        private static async void SendDeviceToCloudMessagesAsync()
        {
            decimal currentTemperature = 26; //starting temperature in Celsius
            int messageId = 1;
            Random rand = new Random(DateTime.Now.Millisecond);

            while (true)
            {
                if (_sendTelemetry)
                {
                    //Generate a random number
                    decimal delta = (decimal)Math.Round(rand.NextDouble(), 2);
                    //Odd numbers positive, Even negative
                    delta = delta * (delta * 100 % 2 == 0 ? -1 : 1);
                    //increase or decrease the temperature by a random amount
                    currentTemperature += delta;

                    var telemetryDataPoint = new
                    {
                        deviceId = deviceId,
                        messageId = messageId++,
                        temperature = currentTemperature,
                    };

                    string messageString = JsonConvert.SerializeObject(telemetryDataPoint);
                    var message = new Message(Encoding.ASCII.GetBytes(messageString));

                    // Add a custom application property to the message.
                    // An IoT hub can filter on these properties without access to the message body.
                    message.Properties.Add("temperatureAlert", currentTemperature > 28 ? "true" : "false");

                    await deviceClient.SendEventAsync(message);
                    Console.WriteLine($"{DateTime.Now} > Sending message: {messageString}");
                }
                await Task.Delay(freq);
            }
        }

        /// <summary>
        ///     Background task for checking for incoming messages from service.
        /// </summary>
        private static async void ReceiveCloudToDeviceAsync()
        {
            while (true)
            {
                //Receive C2D Message, timeout after 10 seconds
                Message receivedMessage = await deviceClient.ReceiveAsync(TimeSpan.FromSeconds(10));
                if (receivedMessage == null)
                    continue;

                string payload = Encoding.ASCII.GetString(receivedMessage.GetBytes());
                dynamic message = JsonConvert.DeserializeObject(payload);
                ConsoleWrite($"\n{payload}", ConsoleColor.Yellow);
                //Let IoT Hub know message was successfully received
                await deviceClient.CompleteAsync(receivedMessage);
            }
        }

        private static async Task<MethodResponse> StopTelemetry(MethodRequest methodRequest, object userContext)
        {
            _sendTelemetry = false;
            ConsoleWrite($"\n{ DateTime.Now} Telemetry Stopped", ConsoleColor.Blue);

            twinProperties["Telemetry"] = "Stopped";
            await deviceClient.UpdateReportedPropertiesAsync(twinProperties);
            var result = "{'message':'Stop Succeeded'}";

            //Send response back to caller
            return new MethodResponse(Encoding.UTF8.GetBytes(result), 200);
        }

        private static async Task<MethodResponse> StartTelemetry(MethodRequest methodRequest, object userContext)
        {
            _sendTelemetry = true;
            ConsoleWrite($"\n{ DateTime.Now} Telemetry Started", ConsoleColor.Blue);

            twinProperties["Telemetry"] = "Started";
            await deviceClient.UpdateReportedPropertiesAsync(twinProperties);
            var result = "{'message':'Start Succeeded'}";

            //Send response back to caller
            return new MethodResponse(Encoding.UTF8.GetBytes(result), 200);
        }

        private static async Task<MethodResponse> UploadFileAsync(MethodRequest methodRequest, object userContext)
        {
            return await Task.FromResult(new MethodResponse(0));
        }

        /// <summary>
        ///     Notification when a Desired Twin Property Changes
        /// </summary>
        /// <param name="desiredProperties"></param>
        /// <param name="userContext"></param>
        /// <returns></returns>
        private static async Task OnDesiredPropertyChanged(TwinCollection desiredProperties, object userContext)
        {
            ConsoleWrite("\nDesired property change:", ConsoleColor.Green);
            ConsoleWrite(JsonConvert.SerializeObject(desiredProperties), ConsoleColor.Green);

            //Update telemetry generation frequency if Desired property 'freq' exists
            if (desiredProperties.Contains("freq"))
            {
                int freq = (int)desiredProperties["freq"];
                if (freq > 0)
                {
                    ConsoleWrite($"Telemetry frequency changed to {freq}ms", ConsoleColor.Green);
                    Program.freq = freq;
                    //Update reported property to reflect new telemetry frequency
                    twinProperties["freq"] = Program.freq;
                    deviceClient.UpdateReportedPropertiesAsync(twinProperties).Wait();
                }
            }
        }

        private static void ConsoleWrite(string message, ConsoleColor? color = null)
        {
            if (color.HasValue)
                Console.ForegroundColor = color.Value;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }
}