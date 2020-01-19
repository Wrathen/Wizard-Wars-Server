using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Server
{
    class Player
    {
        public bool isRed;
        public IPEndPoint ip;

        public Player(bool isRed, IPEndPoint ip)
        {
            this.isRed = isRed;
            this.ip = ip;
        }
    }
    class Server
    {
        static Player RedPlayer;
        static Player BluePlayer;
        static UdpClient server;
        static bool[] EntitySlots = new bool[256];

        static void Main()
        {
            // Create UDP client
            string Server_IPAddress = "192.168.1.104";
            int Server_Port = 2508;

            server = new UdpClient(new IPEndPoint(IPAddress.Parse(Server_IPAddress), Server_Port));
            Console.WriteLine("Started UDP Server: " + Server_Port);

            // Start async receiving
            server.BeginReceive(DataReceived, server);
            Console.ReadLine();
        }
        private static void DataReceived(IAsyncResult ar)
        {
            UdpClient c = (UdpClient)ar.AsyncState;
            IPEndPoint receivedIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
            Byte[] receivedBytes = c.EndReceive(ar, ref receivedIpEndPoint);

            c.BeginReceive(DataReceived, ar.AsyncState);
            ParsePlayerMessage(receivedIpEndPoint, receivedBytes);
        }
        private static void StartTheGame()
        {
            SendMessageTo(RedPlayer.ip, new object[] { Action.Login, true });
            SendMessageTo(BluePlayer.ip, new object[] { Action.Login, false });
        }
        private static void RestartTheGame()
        {
            RedPlayer = null;
            BluePlayer = null;
            Console.WriteLine("Resetted the Game");
        }
        private static void ParsePlayerMessage(IPEndPoint c, byte[] b)
        {
            Action action = (Action)b[0];

            if (action != Action.Login)
            {
                if (RedPlayer == null || BluePlayer == null) return;

                bool SenderColor = b[1] == 1;
                Console.WriteLine(action + " " + SenderColor.ToString());

                if (action == Action.Die)
                {
                    if (RedPlayer == null || BluePlayer == null) return;
                    IPEndPoint redPlayerIP = RedPlayer.ip;
                    IPEndPoint bluePlayerIP = BluePlayer.ip;
                    server.Send(b, b.Length, redPlayerIP);
                    server.Send(b, b.Length, bluePlayerIP);

                    RestartTheGame();
                    return;
                }
                else if (action == Action.Cast)
                {
                    if (RedPlayer == null || BluePlayer == null) return;
                    byte EntitySlot = GetAFreeEntitySlot();
                    if (EntitySlot == 255)
                    {
                        Array.Clear(EntitySlots, 0, 256);
                        EntitySlot = GetAFreeEntitySlot();
                    }

                    // Resize the array so we can pass the Entity ID in.
                    Array.Resize(ref b, b.Length + 1);
                    b[b.Length - 1] = EntitySlot;
                    
                    server.Send(b, b.Length, RedPlayer.ip);
                    server.Send(b, b.Length, BluePlayer.ip);
                    return;
                }
                else if (action == Action.Move)
                {
                    if (RedPlayer == null || BluePlayer == null) return;
                    if (SenderColor) server.Send(b, b.Length, BluePlayer.ip);
                    else server.Send(b, b.Length, RedPlayer.ip);
                    return;
                }
                else if (action == Action.EntityExplosion) // Free the Slots.
                {
                    if (RedPlayer == null || BluePlayer == null) return;
                    EntitySlots[b[2]] = false;
                    EntitySlots[b[3]] = false;
                }
                else if (action == Action.Hit) // Free the Slots.
                {
                    if (RedPlayer == null || BluePlayer == null) return;
                    EntitySlots[b[3]] = false;
                }

                // Transfer the data to the players
                if (RedPlayer == null || BluePlayer == null) return;
                server.Send(b, b.Length, RedPlayer.ip);
                server.Send(b, b.Length, BluePlayer.ip);
            }
            else
            {
                Console.WriteLine("New Login from: " + c);
                if (RedPlayer == null)
                    RedPlayer = new Player(true, c);
                else if (BluePlayer == null)
                    BluePlayer = new Player(false, c);

                if (RedPlayer != null && BluePlayer != null)
                    StartTheGame();
            }
        }

        // Entity
        enum EntityTypes
        {
            Fireball = 0,
            Icelance = 1
        }
        public static byte GetAFreeEntitySlot()
        {
            for (byte i = 0; i < EntitySlots.Length; i++)
                if (!EntitySlots[i]) { EntitySlots[i] = true; return i; }
            return 255;
        }
        // Server Stuff
        enum Action
        {
            Login = 0,
            Move = 1,
            Cast = 2,
            Hit = 3,
            Die = 4,
            EntityExplosion = 5
        }
        public static void SendMessageTo(IPEndPoint ip, params object[] list)
        {
            List<byte> Message = new List<byte>();

            foreach (object o in list)
            {
                Type t = o.GetType();
                if (t.Equals(typeof(byte))) Message.AddRange(AssembleByte((byte)o));
                else if (t.Equals(typeof(bool))) Message.AddRange(AssembleBool((bool)o));
                else if (t.Equals(typeof(float))) Message.AddRange(AssembleFloat((float)o));
                else if (t.Equals(typeof(char))) Message.AddRange(AssembleChar((char)o));
                else if (t.Equals(typeof(string))) Message.AddRange(AssembleString((string)o));
                else if (t.Equals(typeof(int))) Message.AddRange(AssembleInt((int)o));
                else if (t.Equals(typeof(ushort))) Message.AddRange(AssembleUShort((ushort)o));
                else if (t.Equals(typeof(Action))) Message.AddRange(AssembleByte((byte)((Action)o)));
                else if (t.Equals(typeof(EntityTypes))) Message.AddRange(AssembleByte((byte)((EntityTypes)o)));
            }

            server.Send(Message.ToArray(), Message.Count, ip);
        }
        public static void SendMessageTo(UdpClient client, string msg)
        {
            byte[] Message = AssembleString(msg);
            client.Send(Message, Message.Length);
        }
        // Utilities
        public static byte[] AssembleByte(byte b)
        {
            return new byte[] { b };
        }
        public static byte[] AssembleBool(bool b)
        {
            return BitConverter.GetBytes(b);
        }
        public static byte[] AssembleFloat(float f)
        {
            return BitConverter.GetBytes(f);
        }
        public static byte[] AssembleChar(char c)
        {
            return Encoding.ASCII.GetBytes(new char[] { c });
        }
        public static byte[] AssembleString(string s)
        {
            return Encoding.ASCII.GetBytes(s);
        }
        public static byte[] AssembleInt(int i)
        {
            byte[] intBytes = BitConverter.GetBytes(i);
            if (BitConverter.IsLittleEndian) Array.Reverse(intBytes);
            return intBytes;
        }
        public static byte[] AssembleUShort(ushort u)
        {
            return BitConverter.GetBytes(u);
        }

        public static byte DisassembleByte(byte[] b, int i = 0)
        {
            return b[i];
        }
        public static bool DisassembleBool(byte[] b, int i = 0)
        {
            return BitConverter.ToBoolean(b, i);
        }
        public static float DisassembleFloat(byte[] b, int i = 0)
        {
            return BitConverter.ToSingle(b, i);
        }
        public static char DisassembleChar(byte[] b, int i = 0)
        {
            return BitConverter.ToChar(b, i);
        }
        public static string DisassembleString(byte[] b, int i = 0)
        {
            return BitConverter.ToString(b, i);
        }
        public static int DisassembleInt(byte[] b, int i = 0)
        {
            return BitConverter.ToInt32(b, i);
        }
        public static ushort DisassembleUShort(byte[] b, int i = 0)
        {
            return BitConverter.ToUInt16(b, i);
        }
    }
}