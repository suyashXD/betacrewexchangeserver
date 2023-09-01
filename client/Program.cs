using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;

class Utility
{
    // Helper method to print object properties
    public static void PrintObjectProperties(object obj)
    {
        Type type = obj.GetType();
        PropertyInfo[] properties = type.GetProperties();

        foreach (PropertyInfo property in properties)
        {
            object? value = property.GetValue(obj);
            Console.WriteLine($"{property.Name}: {value}");
        }
    }

    // Get the most significant byte of an integer
    public static byte GetMostSignificantByte(int number)
    {
        byte[] bytes = new byte[4];

        bytes[0] = (byte)(number >> 24);
        bytes[1] = (byte)(number >> 16);
        bytes[2] = (byte)(number >> 8);
        bytes[3] = (byte)number;

        return bytes[3];
    }

    // Find missing packet sequences
    public static ArrayList FindMissingPackets(ArrayList packetSequenceList, int maxPacketSequence)
    {
        ArrayList missingPacketSequenceList = new ArrayList();
        packetSequenceList.Sort();

        for (int i = 1; i <= maxPacketSequence; i++)
        {
            if (packetSequenceList.BinarySearch(i) < 0)
            {
                missingPacketSequenceList.Add(i);
            }
        }

        return missingPacketSequenceList;
    }

    // Extract StockTickerData from a buffer
    public static StockTickerData? ExtractStockDataFromBuffer(byte[] buffer)
    {
        if (buffer == null || buffer.Length < 17)
        {
            return null;
        }

        StockTickerData tickerData = new StockTickerData
        {
            Symbol = Encoding.ASCII.GetString(buffer, 0, 4),
            BuySellIndicator = Encoding.ASCII.GetString(buffer, 4, 1),
            Quantity = ToInt32BigEndian(buffer, 5),
            Price = ToInt32BigEndian(buffer, 9),
            PacketSequence = ToInt32BigEndian(buffer, 13)
        };

        return tickerData;
    }

    // Convert bytes to a big-endian integer
    private static int ToInt32BigEndian(byte[] buf, int i)
    {
        return (buf[i] << 24) | (buf[i + 1] << 16) | (buf[i + 2] << 8) | buf[i + 3];
    }
}

// Represents stock ticker data
public class StockTickerData
{
    public string? Symbol { get; set; }
    public string? BuySellIndicator { get; set; }
    public int Quantity { get; set; }
    public int Price { get; set; }
    public int PacketSequence { get; set; }
}

class Program
{
    const string ServerHost = "localhost";
    const int ServerPort = 3000;
    const string OutputJsonFile = "processed_data.json"; // Fixed output file name

    static void Main(string[] args)
    {
        // Node.js debugger client arguments
        /*
        string nodeDebuggerArguments = "--inspect=localhost:3000 C:\\Users\\zucck\\OneDrive\\Desktop\\betacrewexchangeserver\\betacrew_exchange_server\\main.js";
        // Start the Node.js debugger client
        var nodeDebuggerClient = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "node",
                Arguments = nodeDebuggerArguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        nodeDebuggerClient.OutputDataReceived += (sender, e) => Console.WriteLine(e.Data);
        nodeDebuggerClient.ErrorDataReceived += (sender, e) => Console.WriteLine(e.Data);

        Console.WriteLine("Starting Node.js debugger client...");
        nodeDebuggerClient.Start();
        nodeDebuggerClient.BeginOutputReadLine();
        nodeDebuggerClient.BeginErrorReadLine();
        */

        try
        {
            TcpClient? client = null;
            NetworkStream? stream = null;

            using (client = new TcpClient(ServerHost, ServerPort))
            using (stream = client.GetStream())
            {
                byte[] requestBytes = { 1 };
                stream.Write(requestBytes, 0, requestBytes.Length);

                byte[] buffer = new byte[17]; // Packet size
                int bytesRead;

                List<StockTickerData> tickerDataList = new List<StockTickerData>();
                ArrayList packetSequenceList = new ArrayList();
                int maxPacketSequence = -1;
                ArrayList missingPacketSequenceList = new ArrayList();

                try
                {
                    while (!client.Connected)
                    {
                        Console.WriteLine("Waiting for connection...");
                        Thread.Sleep(100);
                    }

                    Console.WriteLine("Connection established...");

                    byte[] requestPayload = new byte[] { 0x01 };
                    stream.Write(requestPayload, 0, requestPayload.Length);

                    Socket socket = client.Client;
                    if (!socket.Poll(1000, SelectMode.SelectRead))
                    {
                        stream.Write(requestPayload, 0, requestPayload.Length);
                    }

                    stream.Flush();

                    while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        StockTickerData? tickerData = Utility.ExtractStockDataFromBuffer(buffer);

                        if (tickerData != null)
                        {
                            tickerDataList.Add(tickerData);
                            packetSequenceList.Add(tickerData.PacketSequence);

                            if (maxPacketSequence < tickerData.PacketSequence)
                            {
                                maxPacketSequence = tickerData.PacketSequence;
                            }
                        }
                    }

                    missingPacketSequenceList = Utility.FindMissingPackets(packetSequenceList, maxPacketSequence);

                    stream.Close();
                    client.Close();

                    //Console.WriteLine("First Request Successful with " + packetSequenceList.Count + " packets out of " + maxPacketSequence + ".");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("An error occurred: " + ex.Message);
                }

                try
                {
                    client = new TcpClient(ServerHost, ServerPort);
                    stream = client.GetStream();

                    foreach (int missingPacket in missingPacketSequenceList)
                    {
                        Console.WriteLine("missing packet number: " + missingPacket);

                        byte[] requestPayload = new byte[2];
                        requestPayload[0] = 0x02;
                        requestPayload[1] = Utility.GetMostSignificantByte(missingPacket);

                        stream.Write(requestPayload, 0, requestPayload.Length);

                        if ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            StockTickerData? tickerData = Utility.ExtractStockDataFromBuffer(buffer);

                            if (tickerData != null)
                            {
                                tickerDataList.Add(tickerData);
                            }
                        }
                    }

                    tickerDataList.Sort((x, y) => x.PacketSequence.CompareTo(y.PacketSequence));

                    string jsonOutput = JsonConvert.SerializeObject(tickerDataList, Formatting.Indented);

                    File.WriteAllText(OutputJsonFile, jsonOutput); // Overwrite the JSON file

                    stream.Close();
                    client.Close();

                    Console.WriteLine("Second Requests successful.");
                    Console.WriteLine("JSON output generated successfully with " + tickerDataList.Count + " packets.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("An error occurred: " + ex.Message);
                }
            }

            //nodeDebuggerClient.Close();
            Console.WriteLine("Debugger client closed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("An error occurred: " + ex.Message);
        }
    }
}
