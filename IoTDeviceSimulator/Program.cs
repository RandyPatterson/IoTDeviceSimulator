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

        public static void Main(string[] args)
        {
            Console.WriteLine("Simulated device. Press any key to exit.\n");
            deviceClient = DeviceClient.CreateFromConnectionString(deviceConnString, TransportType.Mqtt);
            deviceClient.ProductInfo = "IoT Workshop Simulated Device";

            //Start sending Telemetry
            SendDeviceToCloudMessagesAsync();

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
                Console.WriteLine($"{DateTime.Now} > Sending message: {messageString},{delta}");

                await Task.Delay(freq);
            }
        }

        /// <summary>
        ///     Background task for checking for incoming messages from service.
        /// </summary>
        private static Task ReceiveCloudToDeviceAsync()
        {
            return Task.CompletedTask;
        }

        private static async void SetupDirectMethods()
        {
        }

        private static async Task<MethodResponse> StopTelemetry(MethodRequest methodRequest, object userContext)
        {
            return await Task.FromResult(new MethodResponse(0));
        }

        private static async Task<MethodResponse> StartTelemetry(MethodRequest methodRequest, object userContext)
        {
            return await Task.FromResult(new MethodResponse(0));
        }

        private static async Task<MethodResponse> UploadFileAsync(MethodRequest methodRequest, object userContext)
        {
            return await Task.FromResult(new MethodResponse(0));
        }
    }
}