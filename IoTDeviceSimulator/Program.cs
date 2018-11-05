﻿using Microsoft.Azure.Devices.Client;
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
        private const string deviceConnString = "[Your IoT hub device connection string]";
        private const string deviceId = "DevSim01";
        private static readonly TwinCollection twinProperties = new TwinCollection();
        private static volatile int freq = 5000;
        private static volatile bool _sendTelemetry = true;

        public static void Main(string[] args)
        {
            ConsoleWrite("Simulated device. Press any key to exit.\n");
            deviceClient = DeviceClient.CreateFromConnectionString(deviceConnString, TransportType.Mqtt);
            deviceClient.ProductInfo = "IoT Workshop Simulated Device";


            //Start sending Telemetry as a background thread
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

                    //Send Message to IoT Hub
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

        }

        private static async Task<MethodResponse> StopTelemetry(MethodRequest methodRequest, object userContext)
        {
            //Send response back to caller
            return await Task.FromResult(new MethodResponse(0));
        }

        private static async Task<MethodResponse> StartTelemetry(MethodRequest methodRequest, object userContext)
        {
            //Send response back to caller
            return await Task.FromResult(new MethodResponse(0));
        }

        private static async Task<MethodResponse> UploadFileAsync(MethodRequest methodRequest, object userContext)
        {
            //Send response back to caller
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
            //Send response back to caller
            
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