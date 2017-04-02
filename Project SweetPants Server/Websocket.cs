using PacketClassDLL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using WebSocketSharp.Server;

namespace Project_SweetPants_Server
{
    class Websocket
    {
        
        public static Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
        static private string guid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        static byte[] buffer;
        public Websocket()
        {
            Thread startServer = new Thread(StartWebSocketServer);
            startServer.Start();
        }
        
        void StartWebSocketServer()
        {
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, 8181));
            serverSocket.Listen(5);
            serverSocket.BeginAccept(AcceptCallback, null);
        }

        private static void AcceptCallback(IAsyncResult AR)
        {
            Socket socket;
            
            try
            {
                socket = serverSocket.EndAccept(AR);
            }
            catch (ObjectDisposedException) // I cannot seem to avoid this (on exit when properly closing sockets)
            {
                return;
            }

            buffer = new byte[socket.SendBufferSize];
            
            socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, ReceiveCallback, socket);
            Console.WriteLine("Client connected, waiting for request...");
            serverSocket.BeginAccept(AcceptCallback, null);
        }
        private static void ReceiveCallback(IAsyncResult AR)
        {
            Socket current = (Socket)AR.AsyncState;

            int received;
            string headerResponse = "";
            try
            {
                received = current.EndReceive(AR);
            }
            catch (SocketException)
            {
                ClientData c = Server._clients.Find(x => x.clientSocket == current);
                Server.writeline("Connection error!!! User:" + c.DUID + " has been disconected!", ConsoleColor.DarkRed);
                c.ClientDisconnected(Server.sql);
                Server._clients.Remove(c);
                // Dont shutdown because the socket may be disposed and its disconnected anyway
                return;
            }

            if (received > 0)
            {
                headerResponse = (System.Text.Encoding.UTF8.GetString(buffer)).Substring(0, received);
                
                if (new Regex("^GET").IsMatch(headerResponse))
                {
                    /* Handshaking and managing ClientSocket */
                    
                    var key = headerResponse.Replace("ey:", "`")
                              .Split('`')[1]                     // dGhlIHNhbXBsZSBub25jZQ== \r\n .......
                              .Replace("\r", "").Split('\n')[0]  // dGhlIHNhbXBsZSBub25jZQ==
                              .Trim();

                    // key should now equal dGhlIHNhbXBsZSBub25jZQ==
                    var test1 = AcceptKey(ref key);

                    var newLine = "\r\n";

                    var response = "HTTP/1.1 101 Switching Protocols" + newLine
                         + "Upgrade: websocket" + newLine
                         + "Connection: Upgrade" + newLine
                         + "Sec-WebSocket-Accept: " + test1 + newLine + newLine
                        //+ "Sec-WebSocket-Protocol: chat, superchat" + newLine
                        //+ "Sec-WebSocket-Version: 13" + newLine
                         ;

                    Server.writeline("Recieved: " + headerResponse, ConsoleColor.Gray);

                    // which one should I use? none of them fires the onopen method

                    Server.writeline("Sent: " + response, ConsoleColor.Gray);

                    current.Send(System.Text.Encoding.UTF8.GetBytes(response));

                    Server._clients.Add(new ClientData(current, ClientData.ConnectionType.WebSocket));
                }
                else if (decodeData(buffer, received) == "")
                {
                    Server.writeline("Recieved: " + headerResponse, ConsoleColor.Gray);
                    current.Send(buffer);
                }
                else
                {
                    // wait for client to send a message

                    // once the message is received decode it in different formats
                    string data = decodeData(buffer, received);
                    Packet p = new Packet(data);
                    Server.DataManager(p, current);
                    
                }
                
            }
            try
            {
                current.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, ReceiveCallback, current);
            }
            catch(Exception e)
            {
                Console.WriteLine("Connection Error: " + e);
                current.Close();
            }
            
        }
       
        public static T[] SubArray<T>(T[] data, int index, int length)
        {
            T[] result = new T[length];
            Array.Copy(data, index, result, 0, length);
            return result;
        }

        private static string AcceptKey(ref string key)
        {
            string longKey = key + guid;
            byte[] hashBytes = ComputeHash(longKey);
            return Convert.ToBase64String(hashBytes);
        }

        static SHA1 sha1 = SHA1CryptoServiceProvider.Create();
        private static byte[] ComputeHash(string str)
        {
            return sha1.ComputeHash(System.Text.Encoding.ASCII.GetBytes(str));
        }
        public static string decodeData(byte[] bytes, int bytesRec)
        {

            int second = bytes[1] & 127; // AND 0111 1111
            int maskIndex = 2;
            if (second < 126)
            {
                // length fit in second byte
                maskIndex = 2;
            }
            else if (second == 126)
            {
                // next 2 bytes contain length
                maskIndex = 4;
            }
            else if (second == 127)
            {
                // next 8 bytes contain length
                maskIndex = 10;
            }
            // get mask
            byte[] mask = { bytes[maskIndex], 
                                  bytes[maskIndex+1], 
                                  bytes[maskIndex+2], 
                                  bytes[maskIndex+3]};
            int contentIndex = maskIndex + 4;


            // decode
            byte[] decoded = new byte[bytesRec - contentIndex];
            for (int i = contentIndex, k = 0; i < bytesRec; i++, k++)
            {
                // decoded = byte XOR mask
                decoded[k] = (byte)(bytes[i] ^ mask[k % 4]);
            }
            string data = Encoding.UTF8.GetString(decoded, 0, decoded.Length);

            return data;
        }
        public static byte[] encodeData(string message)
        {
            Byte[] response;
            Byte[] bytesRaw = Encoding.UTF8.GetBytes(message);
            Byte[] frame = new Byte[10];

            Int32 indexStartRawData = -1;
            Int32 length = bytesRaw.Length;

            frame[0] = (Byte)129;
            if (length <= 125)
            {
                frame[1] = (Byte)length;
                indexStartRawData = 2;
            }
            else if (length >= 126 && length <= 65535)
            {
                frame[1] = (Byte)126;
                frame[2] = (Byte)((length >> 8) & 255);
                frame[3] = (Byte)(length & 255);
                indexStartRawData = 4;
            }
            else
            {
                frame[1] = (Byte)127;
                frame[2] = (Byte)((length >> 56) & 255);
                frame[3] = (Byte)((length >> 48) & 255);
                frame[4] = (Byte)((length >> 40) & 255);
                frame[5] = (Byte)((length >> 32) & 255);
                frame[6] = (Byte)((length >> 24) & 255);
                frame[7] = (Byte)((length >> 16) & 255);
                frame[8] = (Byte)((length >> 8) & 255);
                frame[9] = (Byte)(length & 255);

                indexStartRawData = 10;
            }

            response = new Byte[indexStartRawData + length];

            Int32 i, reponseIdx = 0;

            //Add the frame bytes to the reponse
            for (i = 0; i < indexStartRawData; i++)
            {
                response[reponseIdx] = frame[i];
                reponseIdx++;
            }

            //Add the data bytes to the response
            for (i = 0; i < length; i++)
            {
                response[reponseIdx] = bytesRaw[i];
                reponseIdx++;
            }

            return response;
        }
    }
}
    

