﻿using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;
using MultiplayerTypes;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace MultiplayerServer
{
    public class ConnectionController
    {
        public delegate void NewConnectionEvent();
        public NewConnectionEvent newConnectionEventHandler;
        public ConcurrentQueue<Command> commands;
        public ConcurrentDictionary<string, SoftwareIncClient> clients;

        public ConnectionController(NewConnectionEvent eventHandler)
        {
            newConnectionEventHandler = eventHandler;
            commands = new ConcurrentQueue<Command>();
            clients = new ConcurrentDictionary<string, SoftwareIncClient>();
        }

        public void StartListening()
        {
            IPAddress ip = IPAddress.Any;
            IPEndPoint endpoint = new IPEndPoint(ip, 9999);
            TcpListener listener = new TcpListener(endpoint);
            listener.Start();

            ThreadPool.QueueUserWorkItem(new WaitCallback(CommandDistributor), null);

            while (true)
            {
                var newClient = listener.AcceptTcpClient();
                Console.WriteLine("New Client connected.");
                ThreadPool.QueueUserWorkItem(new WaitCallback(ConnectionHandler), newClient);
            }

        }

        public void CommandDistributor(object o)
        {
            while (true)
            {
                Command c;
                if (commands.Count > 0)
                {
                    commands.TryDequeue(out c);
                    if (c != null)
                    {
                        SoftwareIncClient s = SoftwareIncClient.getByName(clients.Values, c.source);
                        Console.WriteLine("Command " + c.commandType + " Issued by " + s.name + " | " + s.id);
                        distribute(c, s);
                    }
                }
            }
        }
        public void ConnectionHandler(object tcp)
        {
            var client = (TcpClient)tcp;
            SoftwareIncClient s = null;
            bool closing = false;
            while (!closing)
            {
                if (client.Connected)
                {
                    try
                    {
                        Packet p = new Packet(client);
                        Command cmd = commandFromPacket(p);
                        switch (cmd.commandType)
                        {
                            case CommandType.Login:
                                s = new SoftwareIncClient(client, cmd.source);
                                SoftwareIncClient.generateId(s);
                                clients.TryAdd(s.id, s);
                                Console.WriteLine("New Login: " + s.name + " | " + s.id);
                                break;
                            case CommandType.Logout:
                                clients.TryRemove(s.id, out s);
                                client.Close();
                                closing = true;
                                Console.WriteLine("Logout: " + s.name + " | " + s.id);
                                break;
                            case CommandType.Hack:
                                break;
                            case CommandType.Blame:
                                break;
                            case CommandType.Update:
                                commands.Enqueue(cmd);
                                Console.WriteLine("New Update: " + s.name + " | " + s.id);
                                break;
                            default:
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Exception Caused by: " + s.name + " | " + s.id);
                        Console.WriteLine(ex.Message);
                        clients.TryRemove(s.id, out s);
                        if (client.Connected)
                        {
                            client.Close();
                        }
                        Console.WriteLine("Removed from active Connections");
                        break;
                    }
                    
                }
            }

        }
        
        public Command commandFromPacket(Packet p)
        {
            Command res;
            BinaryFormatter bf = new BinaryFormatter();
            res = (Command)bf.Deserialize(new MemoryStream(p.data));
            return res;
        }

        public Packet packetFromCommand(Command c)
        {
            Packet res;
            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream ms = new MemoryStream();
            bf.Serialize(ms, c);
            res = new Packet(ms.ToArray());
            return res;
        }
            
        public void distribute(Command c, SoftwareIncClient s)
        {
            Packet p = packetFromCommand(c);
            foreach (var item in clients.Keys)
            {
                switch (c.commandType)
                {
                    case CommandType.Login:
                        break;
                    case CommandType.Logout:
                        break;
                    case CommandType.Hack:
                        break;
                    case CommandType.Blame:
                        break;
                    case CommandType.Update:
                        if (s.id != item)
                        {
                            SoftwareIncClient sic = null;
                            clients.TryGetValue(item, out sic);
                            p.send(sic.client);
                        }
                        break;
                    default:
                        break;
                }
               
            }
        }
    }
}
