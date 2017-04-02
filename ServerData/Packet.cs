using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ServerData;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Net;

namespace ServerData
{
    [Serializable]
    public class Packet
    {
        public List<string> Gdata;
        public int packetInt;
        public bool packetBool;
        public string senderId;
        public PacketType packettype;

        public Packet(PacketType type, string senderId)
        {
            Gdata = new List<string>();
            this.senderId = senderId;
            this.packettype = type;

        }
        public Packet(byte[] packetbytes)
        {
            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream ms = new MemoryStream(packetbytes);

            Packet p = (Packet) bf.Deserialize(ms);
            ms.Close();

            this.Gdata = p.Gdata;
            this.packetInt = p.packetInt;
            this.packetBool = p.packetBool;
            this.senderId = p.senderId;
            this.packettype = p.packettype;

        }

        public byte[] ToBytes()
        {
            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream ms = new MemoryStream();

            bf.Serialize(ms, this);
            byte[] bytes = ms.ToArray();
            ms.Close();

            return bytes;

        }

        public enum PacketType
        {
            Registration,
            DUID,
            Name,
            Info, //temporraty info packet
        }
    }
}
