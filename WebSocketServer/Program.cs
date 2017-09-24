using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace WebSocketServer
{
    class Program
    {
        static void Main(string[] args)
        {
            TcpListener server = new TcpListener(IPAddress.Parse("127.0.0.1"), 100);

            server.Start();
            Console.WriteLine("Server has started on 127.0.0.1:80.{0}Waiting for a connection...", Environment.NewLine);

            TcpClient client = server.AcceptTcpClient();

            Console.WriteLine("A client connected.");
            NetworkStream stream = client.GetStream();

            //enter to an infinite cycle to be able to handle every change in stream
            while (true)
            {
                while (!stream.DataAvailable) ;

                Byte[] bytes = new Byte[client.Available];
                stream.Read(bytes, 0, bytes.Length);
                Console.WriteLine("read " + bytes.Length);
                PrintBytes(bytes);

                String data = Encoding.UTF8.GetString(bytes);
                Console.WriteLine(data);
                if (new Regex("^GET").IsMatch(data))
                {
                    var response = GenerateUpgreadWebSocketHandShackRespond(data);
                    Console.WriteLine(Encoding.UTF8.GetString(response));
                    stream.Write(response, 0, response.Length);
                }
                else
                {
                    var receivedMessage = DecodeReceivedMessage(bytes);
                    Console.WriteLine(receivedMessage);
                    Console.WriteLine("===============================");
                    var send = GenerateSendMessage(receivedMessage);
                    stream.Write(send.ToArray(), 0, send.Count);

                }
            }
        }

        private static List<byte> GenerateSendMessage(string receivedMessage)
        {
            var send = new List<byte>();
            send.Add(0x81);
            if (receivedMessage.Length <= 125)
            {
                send.Add((byte)receivedMessage.Length);
            }
            else
            {
                var lenghBytes = new List<byte>();
                var lengh = receivedMessage.Length;
                if (lengh >> (2 * 8) == 0)
                {
                    lenghBytes.Add(126);
                    for (int i = 1; i >= 0; i--)
                    {
                        lenghBytes.Add((byte)(lengh >> 8 * i & 255));
                    }
                }
                else
                {
                    lenghBytes.Add(127);
                    for (int i = 7; i >= 0; i--)
                    {
                        lenghBytes.Add((byte)(lengh >> 8*i & 255));
                    }
                }
                send.AddRange(lenghBytes);
            }
            send.AddRange(Encoding.UTF8.GetBytes(receivedMessage));
            return send;
        }
        private static string DecodeReceivedMessage(byte[] bytes)
        {
            var lenghtCode = bytes[1] - 128;
            int messageLengh = lenghtCode;
            int decodeKeyStartIndex = 2;
            if (lenghtCode == 126)
            {
                decodeKeyStartIndex = 4;
                messageLengh = (bytes[2] << 8) | bytes[3];
            }
            if (lenghtCode == 127)
            {
                decodeKeyStartIndex = 10;
                var lenghtBytes = bytes.Skip(2).Take(8);
                messageLengh = lenghtBytes.Aggregate(messageLengh, (current, b) => current << 8 + b);
            }
            var decodeKeys = bytes.Skip(decodeKeyStartIndex).Take(4).ToArray();
            var encryptMessage = bytes.Skip(decodeKeyStartIndex + 4).Take(messageLengh).ToArray();
            var decodedMessage = new StringBuilder();
            for (int i = 0; i < encryptMessage.Length; i++)
            {
                decodedMessage.Append((char)(encryptMessage[i] ^ decodeKeys[i % 4]));
            }
            return decodedMessage.ToString();
        }

        private static byte[] GenerateUpgreadWebSocketHandShackRespond(string data)
        {
            Byte[] response = Encoding.UTF8.GetBytes("HTTP/1.1 101 Switching Protocols" +
                                                     Environment.NewLine +
                                                     "Connection: Upgrade" + Environment.NewLine +
                                                     "Upgrade: websocket" + Environment.NewLine +
                                                     "Sec-WebSocket-Accept: " +
                                                     Convert.ToBase64String(
                                                         SHA1.Create().ComputeHash(
                                                             Encoding.UTF8.GetBytes(
                                                                 new Regex(
                                                                     "Sec-WebSocket-Key: (.*)").Match(data).Groups[1].Value.Trim
                                                                     () +
                                                                 "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
                                                                 )
                                                             )
                                                         ) + Environment.NewLine
                                                     + Environment.NewLine);
            return response;
        }

        private static void PrintBytes(byte[] bytes)
        {
            foreach (var b in bytes)
            {
                Console.WriteLine((int)b);
            }
            Console.WriteLine("+++++++++++");
        }
    }
}
