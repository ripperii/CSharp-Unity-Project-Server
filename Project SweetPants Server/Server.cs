using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Net;
using System.Reflection;
using PacketClassDLL;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

namespace Project_SweetPants_Server
{
    
    class Server
    {
        static Socket listenerSocket;
        public static List<ClientData> _clients;
        public static MySQL sql;
        static List<List<string>> _LEVEL_SHEET;
        static double _SHOP_OPENED_COOLDOWN_TIME; // shop opened cooldown time in seconds
        static int _STARTING_GOLD_AMOUNT;
        static int _STARTING_DIAMONDS_AMOUNT;
        static int _GOLD_TO_DIAMONDS_CONVERT_RATE;
        static void Main(string[] args)
        {
            Console.Title = "Project SweetPants Server!!!";

            writeline("Starting Server on IP Address: " + GetIPAddress().ToString(),ConsoleColor.Blue);

            writeline(Assembly.GetExecutingAssembly().FullName,ConsoleColor.Gray);

            listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);

            _clients = new List<ClientData>();

            writeline("Connecting to MySQL DataBase Server...", ConsoleColor.Cyan);
            
            sql = new MySQL();

            Websocket ws = new Websocket();

            writeline("Acquiring Level Sheet...", ConsoleColor.Yellow);

            _LEVEL_SHEET = sql.Select("SELECT Level, Requiered_XP FROM levels_sheet;");

            if (_LEVEL_SHEET.Count > 0 || _LEVEL_SHEET != null)
                writeline("Levels sheet loaded successfully!!!", ConsoleColor.Green);
            else
                writeline("Levels sheet could not be loaded!!!", ConsoleColor.Red);

            writeline("Acquiring Constants Values...", ConsoleColor.Cyan);

            List<List<string>> _consts = sql.Select("SELECT constant_name, value FROM constants_sheet;");

            for(int i = 0; i < _consts.Count; i++)
            {
                switch(_consts[i][0])
                {
                    case "STARTING _GOLD": _STARTING_GOLD_AMOUNT = int.Parse(_consts[i][1]); break;
                    case "STARTING_DIAMONDS": _STARTING_DIAMONDS_AMOUNT = int.Parse(_consts[i][1]); break;
                    case "SHOP_OPEN_COOLDOWN_TIME": _SHOP_OPENED_COOLDOWN_TIME = int.Parse(_consts[i][1]); break;
                    case "GOLD_TO_DIAMONDS_CONVERT_RATE": _GOLD_TO_DIAMONDS_CONVERT_RATE = int.Parse(_consts[i][1]); break;
                }
            }

            writeline("Constants Loaded Successfully!!!", ConsoleColor.Green);
            
            IPEndPoint ip = new IPEndPoint(IPAddress.Parse(GetIPAddress()), 4242);
            
            listenerSocket.Bind(ip);
            
            Thread listenThread = new Thread(ListenThread);
            
            //Thread checkConnectionStatusThread = new Thread(CheckConnectionStatus);

            listenThread.Start();
          
            //checkConnectionStatusThread.Start();
        }
        public static void writeline(object o, ConsoleColor c)
        {
            Console.ForegroundColor = c;
            Console.WriteLine(o);
            Console.ResetColor();
        }
        static void ListenThread()
        {
            while (true)
            {
                writeline("Waiting for sync users to connect...",ConsoleColor.Yellow);
                listenerSocket.Listen(0);
                _clients.Add(new ClientData(listenerSocket.Accept(),ClientData.ConnectionType.Socket));
            }
        }
        
        static void CheckConnectionStatus()
        {
            while(true)
            {
                for (int i = 0; i < _clients.Count; i++)
                {
                    try
                    {
                        _clients[i].connected = false;
                        _clients[i].Send(new Packet(1, PacketType.connected));
                    }
                    catch(ObjectDisposedException)
                    {
                        continue;
                    }
                }
                writeline("Connection Packet Sent!!!",ConsoleColor.Blue);
                Thread.Sleep(15000);
                Console.WriteLine(_clients.Count);
                for(int i = 0;i<_clients.Count;i++)
                {
                    ClientData c = _clients[i];
                    if (!c.connected)
                    {
                        writeline("Server has found a not connected user: " + c.DUID, ConsoleColor.Cyan);
                        c.clientSocket.Close();
                        c.clientThread.Abort();
                    }
                }
            }
        }
        static bool SocketConnected(Socket s)
        {
            bool part1 = s.Poll(1000, SelectMode.SelectRead);
            bool part2 = (s.Available == 0);
            if (part1 && part2)
                return false;
            else
                return true;
        }
        public static bool IsSocketStillConnected(Socket socket)
        {
            bool connected = true;
            bool blockingState = socket.Blocking;
            try
            {
                byte[] tmp = new byte[1];
                // Setting Blocking to false makes the execution not to wait until it's complete
                socket.Blocking = false;
                socket.Send(tmp, 0, 0);
            }
            catch (SocketException)
            {
                connected = false;
            }
            finally
            {
                socket.Blocking = blockingState;
            }
            return connected;
        }
        public static void DATA_IN(object cSocket)
        {
            Socket clientSocket = (Socket)cSocket;
            try
            {
                try
                {
                    byte[] buffer;
                    int readBytes;
                    //string xml = "";
                    //string[] x = new string[2];
                    Console.WriteLine("Getting data...");
                    while (true)
                    {
                        if (clientSocket.SendBufferSize > 0)
                        {
                            buffer = new byte[clientSocket.SendBufferSize];
                            readBytes = clientSocket.Receive(buffer);

                            if (readBytes > 0)
                            {
                                
                                /* XML Data
                                xml += XMLClass.bytesToXML(buffer);
                                if(xml.Contains("<EOF/>"))
                                {
                                    x = xml.Split(new string[1]{"<EOF/>"}, 2, StringSplitOptions.None);
                                    xml = x[0];
                                }*/
                                Packet p = new Packet(buffer);
                                DataManager(p, clientSocket);
                            }
                            // xml = x[1];
                        }
                    }
                } // WARNING both catch function have to be write into one
                catch(SocketException)
                {
                    ClientData c = _clients.Find(x => x.clientSocket == clientSocket);
                    writeline("Socket Error!!! User:" + c.DUID + " has been disconected!", ConsoleColor.DarkRed);
                    c.ClientDisconnected(sql);
                    _clients.Remove(c);
                }
            }
            catch(ThreadAbortException)
            {
                ClientData c = _clients.Find(x => x.clientSocket == clientSocket);
                writeline("Thread Error!!! User:" + c.DUID + " has been disconected!", ConsoleColor.DarkRed);
                c.ClientDisconnected(sql);
                _clients.Remove(c);
            }
        }
       
        public static void DataManager(Packet p, Socket c)
        {

            ClientData cd = _clients.Find(x => x.clientSocket == c);
            var isoDateTimeFormat = CultureInfo.InvariantCulture.DateTimeFormat;
            //XML Data
            //List<XMLentry> list = XMLClass.readXML(xml);
            switch(p.type)
            {
                case PacketType.connected:
                    writeline("Received connected packet!!!",ConsoleColor.White);
                    cd.connected = true;
                    break;
                    
                case PacketType.registration:
                    register r = new register().objectToClass(p.obj);
                    
                    List<List<string>> data = sql.Select("SELECT UniqueId, name FROM user WHERE UniqueId= \"" + r.DUID + "\";");
                    for (int i = 0; i < data.Count;i++ )
                        for(int j=0;j< data[i].Count;j++)
                        {
                            writeline("Data: " + data[i][j], ConsoleColor.White);
                        }

                        
                        cd.DUID = r.DUID;
                        cd.device = r.device;
                        cd.firmware = r.firmware;
                        cd.country = r.country;


                        writeline("DUID: " + cd.DUID + " Device: " + cd.device + " Firmware: " + cd.firmware + " Country: " + cd.country, ConsoleColor.White);

                        

                        //XML Data // List<XMLentry> dt = XMLClass.readXML(data);
                        if (data.Count < 1 || data == null)
                        {
                            //XML Data
                            //cd.DUID = list[1].Attributes[0].Value;
                            writeline("DUID received: " + r.DUID, ConsoleColor.White);
                            
                            AddNewUser(cd.DUID,cd.firmware,cd.device, cd.country);
                            
                            Packet pack = new Packet(PacketType.GetName);

                            cd.Send(pack);
                        }
                        else if(data[0][1] == null || data[0][1] == "" || data[0][1] == "NULL")
                        {
                            writeline("User's name is empty!", ConsoleColor.Red);
                            Packet pack = new Packet(PacketType.Name);
                            // Send user information
                            cd.Send(pack);

                        }
                        else
                        {
                            
                            cd.name = data[0][1];
                            getShopCDTime(cd);
                            getAndSendUserDataToUser(cd);
                            getAndSendStorageDataToUser(cd);

                            List<List<string>> savedata = new List<List<string>>();
                            Console.WriteLine("Current Shop ID: " + cd.idshop);
                            savedata = sql.Select("SELECT col1, col2, col3, col4, col5, col6, col7 FROM pending_adventurer WHERE shopid=\"" + cd.idshop + "\";");
                            try
                            {
                                if (savedata.Count > 0)
                                {
                                    cd.adventurerList = savedata;
                                    cd.Send(new Packet(cd.adventurerList, PacketType.adventurer));
                                }
                                else
                                {
                                    writeline("No pending Adventurer Confirmations.", ConsoleColor.Green);
                                }
                            }
                            catch(Exception)
                            {
                                writeline("No pending Adventurer Confirmations.", ConsoleColor.Green);
                            }
                        }
                    break;
                case PacketType.Name: 
                    cd.name = (string) p.obj;
                    sql.Update("UPDATE user SET name=\"" + cd.name + "\" WHERE UniqueId=\"" + cd.DUID + "\";");
                    writeline("User has send name:" + cd.name, ConsoleColor.Blue);
                    
                    getShopCDTime(cd);
                    getAndSendUserDataToUser(cd);
                    getAndSendStorageDataToUser(cd);
                    break;
                case PacketType.shopOpened:
                    writeline("Shop opened in: "+ DateTime.Now.ToString(), ConsoleColor.Blue);
                    List<List<string>> t = new List<List<string>>();
                    t = sql.Select("SELECT cooldown FROM shop WHERE userid=(SELECT iduser FROM user WHERE UniqueId=\"" + cd.DUID + "\");");
                    try
                    {
                        DateTime time = DateTime.Parse(t[0][0]);
                        if (time > DateTime.Now)
                        {
                            getShopCDTime(cd);
                            break;
                        }
                    }
                    catch(FormatException f)
                    {
                        Console.WriteLine("DateTime Format Exeption: " + f + " With time: " + t[0][0] + " Client: " + cd.name);
                    }
                    
                    cd.shopOpened = DateTime.Now + TimeSpan.FromSeconds(_SHOP_OPENED_COOLDOWN_TIME);
                    
                    sql.Update("UPDATE shop SET cooldown=\"" + cd.shopOpened.ToString(isoDateTimeFormat.SortableDateTimePattern) + "\" WHERE userid=(SELECT iduser FROM user WHERE UniqueId=\"" + cd.DUID + "\");");
                    cd.Send(new Packet(_SHOP_OPENED_COOLDOWN_TIME, PacketType.getShopCDTime));

                    getAndSendAdventurerToUser(cd);
                    
                    break;
                case PacketType.getShopCDTime:
                    getShopCDTime(cd);
                    break;
                case PacketType.addXP:

                    cd.xp += (int) p.obj;
                    int neededXP = int.Parse(_LEVEL_SHEET.Find(x => x[0] == cd.level.ToString())[1]);
                    if (cd.xp > neededXP)
                    {
                        cd.xp -= neededXP;
                        cd.level++;
                    }
                    else if(cd.xp == neededXP)
                    {
                        cd.level++;
                        cd.xp = 0;
                    }
                    
                    sendUserDataToUser(cd);
                    break;
                case PacketType.getItemsinStorage:

                    sendStorageDataToUser(cd);

                    break;

                case PacketType.adventurerConfirmation:

                    bool b = (bool)p.obj;
                                        
                    if(b)
                    {
                        int price = 0;
                        for (int i = 0; i < cd.adventurerList.Count;i++ )
                            price += int.Parse(cd.adventurerList[i][3]) * int.Parse(cd.adventurerList[i][5]);
                        
                        if (price > cd.gold)
                        {

                            cd.pendingNEC = new List<string>();
                            cd.pendingNEC.Add(_GOLD_TO_DIAMONDS_CONVERT_RATE.ToString());
                            cd.pendingNEC.Add(cd.gold.ToString());
                            cd.pendingNEC.Add(price.ToString());
                            
                            cd.Send(new Packet(cd.pendingNEC, PacketType.NEC));
                        }
                        else
                        {
                            for (int i = 0; i < cd.adventurerList.Count; i++)
                            {

                                StorageItem si = cd.storageItems.Find(x => x.item.id == cd.adventurerList[i][6]);

                                if (si == null)
                                {

                                    Item it = new Item(sql.Select("SELECT * FROM items WHERE iditems=\"" + cd.adventurerList[i][6] + "\";"));
                                    cd.storageItems.Add(new StorageItem(cd.idshop, int.Parse(cd.adventurerList[i][5]), false, 0, 0, it));
                                    sql.Insert("INSERT INTO storage (shopid, itemid, amount, forSale, amountForSale) VALUES (\"" + cd.idshop + "\", \"" + cd.adventurerList[i][6] + "\", \"" + cd.adventurerList[i][5] + "\", 0, 0 )");
                                }
                                else
                                {
                                    writeline("User: " + cd.name + " has " + si.amount + "X " + si.item.name + " before transaction.", ConsoleColor.Cyan);
                                    si.amount += int.Parse(cd.adventurerList[i][5]);

                                    string strq = "UPDATE storage SET amount =\"" + si.amount + "\" WHERE shopid =\"" + cd.idshop + "\" AND itemid=\"" + cd.adventurerList[i][6] + "\";";
                                    sql.Update(strq);
                                    writeline("Query: " + strq, ConsoleColor.Yellow);
                                    writeline("User: " + cd.name + " has " + si.amount + "X " + si.item.name + " after transaction.", ConsoleColor.Cyan);
                                }

                            }

                            cd.gold -= price;

                            sql.Update("UPDATE user SET gold=\"" + cd.gold + "\" WHERE UniqueId=\"" + cd.DUID + "\";");

                            sendUserDataToUser(cd);
                            sendStorageDataToUser(cd);

                            cd.Send(new Packet(0, PacketType.adventurerConfirmation));

                            sql.Delete("DELETE FROM pending_adventurer WHERE shopid = \"" + cd.idshop + "\";");
                        }
                    }
                    
                    
                    break;
                case PacketType.shopCDTimeCheat:
                    writeline("Shop CD time cheat Activated!!!", ConsoleColor.Blue);
                                        
                    cd.shopOpened = DateTime.Now;
                    
                    sql.Update("UPDATE shop SET cooldown=\"" + cd.shopOpened.ToString(isoDateTimeFormat.SortableDateTimePattern) + "\" WHERE userid=(SELECT iduser FROM user WHERE UniqueId=\"" + cd.DUID + "\");");
                    cd.Send(new Packet(0, PacketType.getShopCDTime));
                    break;
                case PacketType.putItemOnSale:

                    List<string> temp = new List<string>();
                    temp = (List<string>)p.obj;
                    writeline("User: " + cd.name + " put item: " + temp[2] +" " + temp[0] + "X on Sale for: " + temp[1],ConsoleColor.Cyan );
                    putItemOnSale(cd, temp[2], int.Parse(temp[1]), int.Parse(temp[0]));

                    break;
                case PacketType.NECConfirm:
                    double convertPrice = (double.Parse(cd.pendingNEC[2]) - cd.gold) / double.Parse(cd.pendingNEC[0]);

                    convertPrice = Math.Ceiling(convertPrice);

                    cd.diamonds -= (int)convertPrice;
                    cd.gold += int.Parse(cd.pendingNEC[2]) - cd.gold;
                    writeline("NEC Confirmation: " + (int.Parse(cd.pendingNEC[2]) - cd.gold) + " gold for " + convertPrice + " Diamonds", ConsoleColor.Green);
                    sendUserDataToUser(cd);
                    break;
            }
            //_clients.Find(x => x.clientSocket == c).SetClientData(cd);
        }
       
        public static void AddNewUser(string DUID, string fw, string device, string country)
        {
            try
            {
                sql.Insert("INSERT INTO countries (name) VALUES (\"" + country + "\");");
            }
            catch(Exception e)
            {
                writeline("Country: " + country + " exists in database!", ConsoleColor.Cyan);
            }
            sql.Insert("INSERT INTO user (UniqueId, firmware, device, countryId, Gold, Diamonds) VALUES (\"" + DUID + "\", \"" + fw + "\", \"" + device + "\",(SELECT idCountries FROM countries WHERE name = \"" + country + "\"), \"" + _STARTING_GOLD_AMOUNT + "\", \"" + _STARTING_DIAMONDS_AMOUNT + "\");");
        }
        public static string GetIPAddress()
        {
            IPAddress[] ips = Dns.GetHostAddresses(Dns.GetHostName());

            foreach (IPAddress ip in ips)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    return ip.ToString();
            }
            return "127.0.0.1";
        }
        public static void getShopCDTime(ClientData cd)
        {
            List<List<string>> temp = new List<List<string>>();
            string seconds = "";
            temp = sql.Select("SELECT idshop, cooldown FROM user, shop WHERE userid=iduser AND UniqueId=\"" + cd.DUID + "\";");
            try
            {
                cd.idshop = temp[0][0];
                cd.shopOpened = DateTime.Parse(temp[0][1]);
                seconds = (cd.shopOpened - DateTime.Now).TotalSeconds.ToString();
            }
            catch(FormatException)
            {
                seconds = "0";

                cd.shopOpened = DateTime.Now.Subtract(new TimeSpan(0, 0, 0, int.Parse(_SHOP_OPENED_COOLDOWN_TIME.ToString())));
            }
            writeline("Shop will close in: " + seconds + " seconds!", ConsoleColor.Blue);
            cd.Send(new Packet(seconds, PacketType.getShopCDTime));
        }
        public static void getAndSendUserDataToUser(ClientData cd)
        {
            List<List<string>> temp = new List<List<string>>();

            temp = sql.Select("SELECT Level, XP, Gold, Diamonds FROM user WHERE UniqueId=\"" + cd.DUID + "\";");

            cd.level = int.Parse(temp[0][0]);
            cd.xp = int.Parse(temp[0][1]);
            cd.gold = int.Parse(temp[0][2]);
            cd.diamonds = int.Parse(temp[0][3]);

            temp[0].Add(_LEVEL_SHEET.Find(x => x[0] == temp[0][0])[1]);

            cd.Send(new Packet(temp, PacketType.userData));
        }
        public static void sendUserDataToUser(ClientData cd)
        {
            List<List<string>> temp = new List<List<string>>();

            temp.Add(new List<string>());

            temp[0].Add(cd.level.ToString());
            temp[0].Add(cd.xp.ToString());
            temp[0].Add(cd.gold.ToString());
            temp[0].Add(cd.diamonds.ToString());

            temp[0].Add(_LEVEL_SHEET.Find(x => x[0] == temp[0][0])[1]);

            cd.Send(new Packet(temp, PacketType.userData));
        }
        public static void getAndSendStorageDataToUser(ClientData cd)
        {
            List<List<string>> temp = new List<List<string>>();

            temp = sql.Select("SELECT name, description, rarity, price, priceCurrencyType, Crafting, amount, forSale, amountForSale, idstorage, iditems, priceOnSale FROM storage, items WHERE shopid = \"" + cd.idshop + "\" AND iditems = itemid;");

            writeline("User " + cd.name + " has amount of items in storage - " + temp.Count, ConsoleColor.Magenta);

            cd.storageItems = new List<StorageItem>();

            for(int i = 0; i < temp.Count; i++)
            {
                Item it = new Item(temp[i][0], temp[i][1], temp[i][2], int.Parse(temp[i][3]), temp[i][4], Convert.ToBoolean(int.Parse(temp[i][5])),temp[i][10]);
                if (temp[i][11] == "" || temp[i][11] == null) temp[i][11] = "0";
                StorageItem st = new StorageItem(temp[i][9], int.Parse(temp[i][6]), Convert.ToBoolean(int.Parse(temp[i][7])), int.Parse(temp[i][8]),int.Parse(temp[i][11]),it);
                
                cd.storageItems.Add(st);
            }

            writeline("StorageItems sent: " + cd.storageItems.Count + " To user: " + cd.name + " and shopid: " + cd.idshop , ConsoleColor.Cyan);

            cd.Send(new Packet(temp, PacketType.getItemsinStorage));
        }
        public static void sendStorageDataToUser(ClientData cd)
        {
            List<List<string>> temp = new List<List<string>>();

            for (int i = 0; i < cd.storageItems.Count; i++)
            {
                temp.Add(cd.storageItems[i].StorageItemToList());
            }
            cd.Send(new Packet(temp, PacketType.getItemsinStorage));
        }
        public static void getAndSendAdventurerToUser(ClientData cd)
        {
            string adventurer = sql.Select("SELECT idAdventurer FROM adventurer order by RAND() limit 1")[0][0];
            List<List<string>> temp = sql.Select("SELECT a.name, a.description, a.itemsMin, a.itemsMax, i.name, i.price, i.priceCurrencyType, itemAmountMin, itemAmountMax, i.iditems FROM adventurer a, adventureritems, items i WHERE Adventurerid = idAdventurer AND iditems = adItemid AND idAdventurer = \"" + adventurer + "\" ORDER BY RAND();");

            Random rnd = new Random();
            int items = rnd.Next(int.Parse(temp[0][2]), int.Parse(temp[0][3]));

            cd.adventurerList = new List<List<string>>();

            for (int i = 0; i < items; i++)
            {
                cd.adventurerList.Add(new List<string>());
                cd.adventurerList[i].Add(temp[i][0]);
                cd.adventurerList[i].Add(temp[i][1]);
                cd.adventurerList[i].Add(temp[i][4]);
                cd.adventurerList[i].Add(temp[i][5]);
                cd.adventurerList[i].Add(temp[i][6]);
                cd.adventurerList[i].Add(rnd.Next(int.Parse(temp[i][7]), int.Parse(temp[i][8])).ToString());
                cd.adventurerList[i].Add(temp[i][9]);

                sql.Insert("INSERT INTO pending_adventurer (shopid, col1, col2, col3, col4, col5, col6, col7) VALUES (\"" + cd.idshop + "\",\"" + cd.adventurerList[i][0] + "\",\"" + cd.adventurerList[i][1] + "\",\"" + cd.adventurerList[i][2] + "\",\"" + cd.adventurerList[i][3] + "\",\"" + cd.adventurerList[i][4] + "\",\"" + cd.adventurerList[i][5] + "\",\"" + cd.adventurerList[i][6] + "\");");

            }

            writeline("Adventurer sent: " + cd.adventurerList[0][0] + " To user: " + cd.name, ConsoleColor.Cyan);


            cd.Send(new Packet(cd.adventurerList, PacketType.adventurer));
        }

        public static void putItemOnSale(ClientData cd,  string itemid, int price, int numberOfItemsOnSale)
        {
            StorageItem si = cd.storageItems.Find(x => x.item.id == itemid);

            if(si!=null)
            {
                si.priceOnSale = price;
                si.amountForSale += numberOfItemsOnSale;
                si.forSale = true;
                sql.Update("UPDATE storage SET priceOnSale= \"" + price + "\", amountForSale=\"" + numberOfItemsOnSale + "\", forSale=\"1\" WHERE shopid=\"" + cd.idshop + "\" AND itemid=\"" + si.item.id +"\";" );
            }
            else
            {
                writeline("Item: "+ itemid +" was not fount in storage of user: " + cd.name + " !!!", ConsoleColor.Red);
            }
        }
    }
   
    
    class ClientData
    {
        
        public Socket clientSocket;
        public ConnectionType conType;
        public Thread clientThread;
        public string idshop;
        public bool connected;
        public string id;
        public string name;
        public string DUID;
        public string device;
        public string firmware;
        public string country;
        public int xp;
        public int level;
        public int gold;
        public int diamonds;
        public DateTime shopOpened;
        public List<StorageItem> storageItems;
        public List<List<string>> adventurerList;
        public List<string> pendingNEC;
        
        public ClientData()
        {
            id = Guid.NewGuid().ToString();
            connected = true;
            clientThread = new Thread(Server.DATA_IN);
            clientThread.Start(clientSocket);
        }
        public ClientData(Socket clientSocket, ConnectionType t)
        {
            Server.writeline("Adding new client!", ConsoleColor.Yellow);

            this.clientSocket = clientSocket;
            id = Guid.NewGuid().ToString();
            this.connected = true;
            this.conType = t;

            // SendRegistrationPacket();
            
              // Need To add Code for new thread for encoded websocket for Unity WebGL Apps
            if (t == ConnectionType.Socket)
            {
                clientThread = new Thread(Server.DATA_IN);
                clientThread.Start(clientSocket);
                this.SendRegistrationPacket();
            }
            else
            {
                Server.writeline("Client connected through a WebSocket!!!", ConsoleColor.Yellow);
                //this.SendRegistrationPacket();
            }
           
        }
        public void SendRegistrationPacket()
        {
            /*XML Data
            string p = "<registration></registration>";
            byte[] bites = XMLClass.XMLtoBytes(p);
            Console.WriteLine("Bytes sent: " + bites);
            clientSocket.Send(bites);
             */
            Packet p = new Packet(0, PacketType.registration);
            this.Send(p);
        }
        public void SetClientData(ClientData c)
        {
            this.idshop = c.idshop;
            this.clientSocket = c.clientSocket;
            this.clientThread = c.clientThread;
            this.connected = c.connected;
            this.id = c.id;
            this.firmware = c.firmware;
            this.device = c.device;
            this.name = c.name;
            this.DUID = c.DUID;
            this.xp = c.xp;
            this.level = c.level;
            this.gold = c.gold;
            this.diamonds = c.diamonds;
            this.shopOpened = c.shopOpened;
            this.storageItems = c.storageItems;
            this.adventurerList = c.adventurerList;
            this.pendingNEC = c.pendingNEC;
        }

        public void Send(Packet p)
        {
            //Server.writeline("Sent: " + p.obj.ToString() + " Packet Type: " + p.type.ToString(), ConsoleColor.Gray);
            if(this.conType == ConnectionType.Socket)
            {
                this.clientSocket.Send(p.SerializePacket());
            }
            else if(this.conType == ConnectionType.WebSocket)
            {
                this.clientSocket.Send(Packet.encodeData(p.packetXMLSerialization()));
            }
        }
        public void ClientDisconnected(MySQL sql)
        {

            var isoDateTimeFormat = CultureInfo.InvariantCulture.DateTimeFormat;
            sql.Update("UPDATE shop SET cooldown=\"" + this.shopOpened.ToString(isoDateTimeFormat.SortableDateTimePattern) + "\" WHERE userid=(SELECT iduser FROM user WHERE UniqueId=\"" + this.DUID + "\");");
            sql.Update("UPDATE user SET Level = \"" + this.level + "\", XP = \"" + this.xp + "\", Gold = \"" + this.gold + "\", Diamonds = \"" + this.diamonds +"\" WHERE UniqueId = \"" + this.DUID + "\";");
            this.clientSocket.Close();
        
        }
        public enum ConnectionType
        {
            WebSocket,
            Socket
        }
        
    }
    class StorageItem
    {
        public string id;
        public int amount;
        public bool forSale;
        public int amountForSale;
        public int priceOnSale;
        public Item item;

        public StorageItem(string mid, int am, bool fs, int afs, int pos, Item mitem)
        {
            id = mid;
            amount = am;
            forSale = fs;
            amountForSale = afs;
            priceOnSale = pos;
            item = mitem;
        }
        public List<string> StorageItemToList()
        {
            List<string> str = new List<string>();

            str.Add(this.item.id);
            str.Add(this.item.name);
            str.Add(this.item.description);
            str.Add(this.item.rarity);
            str.Add(this.item.price.ToString());
            str.Add(this.item.priceCurrencyType);
            str.Add(this.amount.ToString());
            str.Add(this.forSale.ToString());
            str.Add(this.amountForSale.ToString());
            str.Add(this.priceOnSale.ToString());
            return str;
        }
    }
    class Item
    {
        public string id;
        public string name;
        public string description;
        public string rarity;
        public int price;
        public string priceCurrencyType;
        public bool crafting;

        public Item(string mname, string mdescription, string mrarity, int mprice, string mpriceCurrencyType, bool mcrafting, string mid)
        {
            id = mid;
            name = mname;
            description = mdescription;
            rarity = mrarity;
            price = mprice;
            priceCurrencyType = mpriceCurrencyType;
            crafting = mcrafting;
        }
        public Item(List<List<string>> temp)
        {
            id = temp[0][0];
            name = temp[0][1];
            description = temp[0][2];
            rarity = temp[0][3];
            price = int.Parse(temp[0][4]);
            priceCurrencyType = temp[0][5];
            crafting = Convert.ToBoolean(int.Parse(temp[0][6]));

        }
    }
}
