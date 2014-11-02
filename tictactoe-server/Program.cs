using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace tictactoe_server
{
    class Client
    {
        private readonly TcpClient _client;
        private int _id;
        private string _name;
        private string _marker;
        private bool _isPlayer;

        public Client(TcpClient client)
        {
            _client = client;
            _isPlayer = false;
        }

        public TcpClient GetClient()
        {
            return _client;
        }

        public int GetId()
        {
            return _id;
        }

        public void SetId(int id)
        {
            Console.WriteLine("Player {0} ({1}) has changed their ID from {2} to {3}", _name, _id, _id, id);

            _id = id;
        }

        public string GetName()
        {
            return _name;
        }

        public void SetName(string name)
        {
            if (String.IsNullOrWhiteSpace(name))
                return;

            Console.WriteLine("Player {0} ({1}) has changed their name from {2} to {3}", _name, _id, _name, name);

            _name = name;
        }

        public string GetMarker()
        {
            return _marker;
        }

        public void SetMarker(string marker)
        {
            Console.WriteLine("Player {0} ({1}) has changed their marker from {2} to {3}", _name, _id, _marker, marker);

            _marker = marker;
        }

        public void IsPlayer(bool isPlayer)
        {
            Console.WriteLine("Player {0} ({1}) is now a player.", _name, _id);

            _isPlayer = isPlayer;
        }

        public bool IsPlayer()
        {
            return _isPlayer;
        }
    }

    class Program
    {
        // server connection
        private static TcpListener _listener;

        // all connected users, including wachers
        private static readonly List<Client> Clients = new List<Client>();

        // order to ask the players to make a move. contains a list of indexes to _clients
        private static readonly List<int> PlayerOrder = new List<int>();

        // the current playing whose turn it is
        private static int _currentPlayer;

        // are the x and o markers available for incoming users?
        private static bool _markerXAvailable = true, _markerOAvailable = true;

        // thread lock
        private static readonly Object WriteLock = new Object();

        // the current playing board
        private static string[] _boardState = new[] { " ", " ", " ", " ", " ", " ", " ", " ", " " };

        // is a game in progress?
        private static bool _playingGame;

        static void Main(string[] args)
        {
            // get ip address to connect to
            IPAddress listenAddress = IPAddress.Parse("127.0.0.1");
            int listenPort = 3333;

            // if two parameters were passed in, handle it as an address and port combo
            // if only one parameter was passed in, handle it as just the port
            if (args.Length == 2)
            {
                listenAddress = IPAddress.Parse(args[0]);
                int.TryParse(args[1], out listenPort);
            }
            else if (args.Length == 1)
            {
                int.TryParse(args[0], out listenPort);
            }

            try
            {
                // start tcp listener with specified port
                _listener = new TcpListener(listenAddress, listenPort);
            }
            catch (Exception e)
            {
                Console.WriteLine("Could not create listener: {0}", e.Message);
                Environment.Exit(1);
            }

            Console.WriteLine("Server successfully started...");
            Console.WriteLine("Server info: {0}:{1}", listenAddress, listenPort);
            Console.WriteLine();

            // start connection handler
            HandleConnections();
        }

        private static void HandleConnections()
        {
            // start listening
            _listener.Start();

            Console.WriteLine("Waiting for connections...");

            // infinite loop to accept clients
            for (;;)
            {
                // add accepted client to the list of clients
                TcpClient tcpClient = _listener.AcceptTcpClient();

                // create a user
                Client client = new Client(tcpClient);
                client.SetId(Clients.Count);

                // lock so we can set up users even if a bunch connect at once
                lock (WriteLock)
                {
                    // only assign markers to actual players (ignore watchers)
                    if (PlayerOrder.Count < 2)
                    {
                        string marker = "";

                        // determine whether the user is the x or the o
                        if (_markerXAvailable)
                        {
                            marker = "x";
                            _markerXAvailable = false;
                        }
                        else if (_markerOAvailable)
                        {
                            marker = "o";
                            _markerOAvailable = false;
                        }

                        // set player's marker and as an active player
                        client.SetMarker(marker);
                        client.SetName(String.Format("Player {0}", client.GetId() + 1));
                        client.IsPlayer(true);

                        // add to list of players
                        PlayerOrder.Add(client.GetId());
                    }

                    // add to overall clients (including watchers)
                    Clients.Add(client);
                }

                // start new thread for each client
                Thread clientThread = new Thread(HandleClient);
                clientThread.Start(client);

                // purge disconnected users
                for (int i = Clients.Count - 1; i >= 0; i--)
                {
                    if (!Clients[i].GetClient().Connected)
                    {
                        // reclaim marker if it was an x or an o
                        string marker = Clients[i].GetMarker();

                        if (marker == "x")
                            _markerXAvailable = true;
                        else if (marker == "o")
                            _markerOAvailable = true;

                        // remove from clients and, if a player, from the players list
                        Clients.RemoveAt(i);
                        PlayerOrder.Remove(i);
                    }
                }
            }
        }

        private static void ResetBoard()
        {
            // initialize game board with default values
            _playingGame = true;
            _boardState = new[] { " ", " ", " ", " ", " ", " ", " ", " ", " " };
            _currentPlayer = 0;

            // notify users of changes to the game state
            SendBoardToAllUsers();

            // tell users that the game has started
            SendToAllUsers("CHAT", String.Format("Game has started. {0} is first.", Clients[_currentPlayer].GetName()));

            // server debug message
            Console.WriteLine("Board has been reset.");
        }

        private static void HandleClient(object data)
        {
            Client client = (Client)data;
            TcpClient c = client.GetClient();

            // server debug message
            Console.WriteLine("Client #{0}: {1} ({2}) has connected!", client.GetId(),
                              ((IPEndPoint) client.GetClient().Client.RemoteEndPoint).Address,
                              Dns.GetHostEntry(((IPEndPoint) client.GetClient().Client.RemoteEndPoint).Address).HostName);
            
            // only start the game if there are two players, otherwise notify user that we are waiting
            if (Clients.Count == 2)
            {
                ResetBoard();
            }
            else
            {
                SendToAllUsers("CHAT", "Waiting for another player...");
            }

            // loop until the user disconnects
            while(c.Connected)
            {
                // try to read data from user
                byte[] buffer = new byte[1024];
                int received = 0;

                try
                {
                    received = c.GetStream().Read(buffer, 0, 1024);
                }
                catch (Exception)
                {
                }

                // alive check
                if (!c.Connected)
                    break;

                // convert data from client into a string
                string response = Encoding.UTF8.GetString(buffer, 0, received);

                Console.WriteLine("[From {0} ({1})] {2}", client.GetName(), client.GetId(), response);

                // handle command
                if (response.StartsWith("SET_NAME|")) // player name change
                {
                    response = response.Remove(0, "SET_NAME|".Length);
                    client.SetName(response);

                    // notify users of new player
                    SendToAllUsers("CHAT", String.Format("{0} has connected.", client.GetName()));
                }
                else if (response.StartsWith("CHAT|")) // chat message to everyone
                {
                    response = response.Remove(0, "CHAT|".Length);

                    string name = client.GetName(); // name of player who sent chat message

                    // send command to all users
                    SendToAllUsers("CHAT", String.Format("{0}: {1}", name, response));
                    
                    if (client.IsPlayer() && response.ToLower() == "!restart")
                    {
                        // only allow an actual player to reset the game board
                        ResetBoard();
                    }
                    else if (response.ToLower().StartsWith("!setname "))
                    {
                        // player-requested name change
                        response = response.Remove(0, "!setname ".Length);

                        // check if the name is already in use by another player
                        bool nameAlreadyExists = Clients.Any(x => x.GetName() == response);

                        if (!nameAlreadyExists)
                        {
                            // notify all users that the user has changed their name
                            SendToAllUsers("CHAT", String.Format("{0} has changed their name to {1}.", client.GetName(), response));
                            client.SetName(response);
                        }
                        else
                        {
                            // notify the user that the name is already in use
                            SendToUser(client, "CHAT", String.Format("The name '{0}' is already in use.", response));
                        }
                    }
                    else if (client.IsPlayer() && response.ToLower().StartsWith("!setmarker "))
                    {
                        // player-requested custom marker
                        response = response.Remove(0, "!setmarker ".Length);

                        // store the player's current marker so we reclaim it later and update the board
                        string oldMarker = client.GetMarker();

                        // check if the marker is already being used by another player
                        bool alreadyOwned = false;
                        if (Clients.Any(x => x.GetMarker().Equals(response, StringComparison.CurrentCultureIgnoreCase)))
                        {
                            Console.WriteLine(
                                "Player {0}'s ({1}'s) marker could not be updated from '{2}' to '{3}' because another player already owns that piece.",
                                client.GetName(), client.GetId(), oldMarker, response);

                            // notify user that marker is already in use
                            SendToUser(client, "CHAT", String.Format("The marker '{0}' is already in use.", response));

                            alreadyOwned = true;
                        }

                        if (!alreadyOwned)
                        {
                            // reclaim the x and o markers if possible
                            if (oldMarker == "x")
                                _markerXAvailable = true;
                            else if (oldMarker == "o")
                                _markerOAvailable = true;

                            // set the user's marker
                            client.SetMarker(response);

                            // send chat message to all users notifying them of the new marker
                            SendToAllUsers("CHAT", String.Format("{0} has changed their marker to {1}.", client.GetName(), client.GetMarker()));

                            if (_playingGame)
                            {
                                // update markers already placed on the board state
                                for (int i = 0; i < _boardState.Length; i++)
                                {
                                    if (_boardState[i] == oldMarker)
                                        _boardState[i] = client.GetMarker();
                                }

                                // update all users of the changes
                                SendBoardToAllUsers();
                            }
                        }
                    }
                }
                else if (client.IsPlayer() && response.StartsWith("MARK|"))
                {
                    // don't mark if the wrong player tried to mark something
                    if (PlayerOrder[_currentPlayer] != client.GetId())
                        continue;

                    // don't mark if a game is not in progress
                    if (!_playingGame)
                        continue;

                    // get position that player marked
                    response = response.Remove(0, "MARK|".Length);
                    string[] s = response.Split('|');

                    // try to convert marker position strings to ints
                    int x, y;
                    int.TryParse(s[0], out x);
                    int.TryParse(s[1], out y);

                    // set marker
                    _boardState[(y * 3) + x] = client.GetMarker();

                    // check if a winner has been found
                    bool isWinner = CheckIfWinner(client.GetMarker());
                    SendBoardToAllUsers();

                    // if the player has won, notify all users
                    if (isWinner)
                    {
                        // server debug message
                        Console.WriteLine("Player {0} ({1}) wins!", client.GetName(), client.GetId());

                        // send chat message to everyone
                        SendToAllUsers("CHAT", String.Format("{0} wins!{1}Type !restart to play again.", client.GetName(), Environment.NewLine));

                        // send win/lose status to players
                        NotifyUsersOfWin(client.GetMarker());

                        // no longer handling set marker commands
                        _playingGame = false;
                    }

                    // only change players if the game is in progress
                    if (_playingGame)
                    {
                        // alternate between players
                        _currentPlayer++;
                        if (_currentPlayer > PlayerOrder.Count - 1)
                        {
                            _currentPlayer = 0;
                        }

                        // tell all users whose turn it is
                        SendToAllUsers("CHAT", String.Format("{0}'s turn.", Clients[_currentPlayer].GetName()));
                    }
                }

                // wait a little bit before sending/receiving more data
                Thread.Sleep(100);
            }

            // server debug message
            Console.WriteLine("Client #{0} has disconnected.", client.GetId());

            // let all suers know when someone has disconnected
            SendToAllUsers("CHAT", String.Format("Client #{0} has disconnected.", client.GetId()));
        }

        private static bool CheckIfWinner(string marker)
        {
            // return whether or not the current player is the winner
            bool isWinner = false;

            for (int i = 0; i < 3; i++) // horizontal victory
            {
                if (_boardState[(i * 3) + 0] == marker && _boardState[(i * 3) + 1] == marker && _boardState[(i * 3) + 2] == marker)
                {
                    isWinner = true;
                    break;
                }
            }

            if (!isWinner)
            {
                for (int i = 0; i < 3; i++) // vertical victory
                {
                    if (_boardState[0 + i] == marker && _boardState[3 + i] == marker && _boardState[6 + i] == marker)
                    {
                        isWinner = true;
                        break;
                    }
                }
            }

            if (!isWinner)
            {
                // diag l-r
                if (_boardState[0] == marker && _boardState[4] == marker && _boardState[8] == marker)
                {
                    isWinner = true;
                }
            }

            if (!isWinner)
            {
                // diag r-l
                if (_boardState[2] == marker && _boardState[4] == marker && _boardState[6] == marker)
                {
                    isWinner = true;
                }
            }

            return isWinner;
        }

        private static void SendBoardToAllUsers()
        {
            // serialize the board state and send to all users
            string boardState = _boardState.Aggregate("", (current, s) => current + (s + "|"));

            boardState = boardState.Trim('|');

            SendToAllUsers("MARK", boardState);

            Thread.Sleep(50);
        }

        private static void SendToUser(Client client, string command, string message)
        {
            // send a command to a specific user
            message = "`" + command + "|" + message;

            TcpClient c = client.GetClient();

            // check if alive
            if (!c.Connected)
                return;

            try
            {
                c.Client.Send(Encoding.UTF8.GetBytes(message), SocketFlags.None);
            }
            catch (Exception)
            {
            }

            //Thread.Sleep(50);
        }

        private static void SendToAllUsers(string command, string message)
        {
            // send a command to all users
            message = "`" + command + "|" + message;

            foreach (Client client in Clients)
            {
                TcpClient c = client.GetClient();

                // check if alive
                if (!c.Connected)
                    continue;

                try
                {
                    c.Client.Send(Encoding.UTF8.GetBytes(message), SocketFlags.None);
                }
                catch (Exception)
                {
                }
            }

            //Thread.Sleep(50);
        }

        private static void NotifyUsersOfWin(string marker)
        {
            // notify all players whether or not they've won
            foreach (int player in PlayerOrder)
            {
                Client client = Clients[player];
                TcpClient c = client.GetClient();

                // check if alive
                if (!c.Connected)
                    continue;

                // default = "0" (not winner)
                string winner = "0";

                // set to "1" if the winner's marker matches the player's marker
                if (client.GetMarker() == marker)
                    winner = "1";

                // send winner message to player
                string message = "WINNER|" + winner;

                try
                {
                    c.Client.Send(Encoding.UTF8.GetBytes(message), SocketFlags.None);
                }
                catch (Exception)
                {
                }
            }

            //Thread.Sleep(50);
        }
    }
}
