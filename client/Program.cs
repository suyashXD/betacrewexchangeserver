using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace BetaCrewClientApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            string serverHostname = "localhost"; // Update with the actual server IP or hostname
            int serverPort = 3000;
            List<Packet> receivedPackets = new List<Packet>();
            int lastReceivedSequence = -1;

            try
            {
                using (TcpClient client = new TcpClient())
                {
                    await client.ConnectAsync(serverHostname, serverPort);
                    Console.WriteLine("Connected to the server.");

                    using (NetworkStream stream = client.GetStream())
                    using (StreamWriter writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true })
                    using (BinaryReader reader = new BinaryReader(stream, Encoding.BigEndianUnicode))
                    {
                        // Send the "Stream All Packets" request to the server
                        writer.Write((byte)1);

                        // Receive and process the packets
                        while (true)
                        {
                            byte callType = reader.ReadByte();
                            if (callType == 1)
                            {
                                Packet packet = ReadPacket(reader);

                                // Handle missed sequences
                                if (lastReceivedSequence >= 0)
                                {
                                    for (int missingSeq = lastReceivedSequence + 1; missingSeq < packet.PacketSequence; missingSeq++)
                                    {
                                        RequestMissingPacket(writer, missingSeq);
                                    }
                                }

                                ProcessPacket(packet);
                                receivedPackets.Add(packet);
                                lastReceivedSequence = packet.PacketSequence;
                            }
                            else if (callType == 2)
                            {
                                int resendSeq = reader.ReadByte(); // Read the sequence number to be resent
                                Packet packetToResend = receivedPackets.Find(p => p.PacketSequence == resendSeq);
                                
                                if (packetToResend?.Symbol != null)
                                {
                                    SendPacket(writer, packetToResend);
                                }
                                else
                                {
                                    Console.WriteLine($"Requested packet with sequence number {resendSeq} not found.");
                                }
                            }

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
            }
        }

        static void SendPacket(StreamWriter writer, Packet packet)
        {
            writer.Write((byte)1); // Call type 1 indicates sending a packet
            if (!string.IsNullOrEmpty(packet.Symbol))
            {
                writer.Write(packet.Symbol.ToCharArray());
            }
            writer.Write(packet.BuySellIndicator);
            writer.Write(packet.Quantity);
            writer.Write(packet.Price);
            writer.Write(packet.PacketSequence);
        }

        static void RequestMissingPacket(StreamWriter writer, int missingSequence)
        {
            // Send a "Resend Packet" request to the server for the missing sequence
            writer.Write((byte)2);
            writer.Write((byte)missingSequence);
        }

        static void GenerateJsonOutput(List<Packet> packets)
        {
            try
            {
                // Generate JSON output containing an array of packet objects
                string jsonOutput = JsonSerializer.Serialize(packets, new JsonSerializerOptions
                {
                    WriteIndented = true // Optionally format the JSON for readability
                });

                // Write the JSON output to a file
                File.WriteAllText("output.json", jsonOutput);

                Console.WriteLine("JSON output generated and saved to output.json.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while generating the JSON output: " + ex.Message);
            }
        }

        static Packet ReadPacket(BinaryReader reader)
        {
            Packet packet = new Packet
            {
                Symbol = new string(reader.ReadChars(4)),
                BuySellIndicator = reader.ReadChar(),
                Quantity = reader.ReadInt32(),
                Price = reader.ReadInt32(),
                PacketSequence = reader.ReadInt32()
            };
            return packet;
        }

        static void ProcessPacket(Packet packet)
        {
            // This method is already handling the processing of the packet
            // You're adding the packet to the receivedPackets list and updating lastReceivedSequence
            // You might not need to make any changes here if your requirements are already met
        }
    }

    // Define a class to represent the packet structure
    class Packet
    {
        public string Symbol { get; set; } // Removed the nullable annotation
        public char BuySellIndicator { get; set; }
        public int Quantity { get; set; }
        public int Price { get; set; }
        public int PacketSequence { get; set; }

        public Packet()
        {
            Symbol = string.Empty; // Initialize Symbol with a non-null value
        }
    }
}
