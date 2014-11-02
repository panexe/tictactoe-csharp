using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace tictactoe_client
{
    class Program
    {
        private static TcpClient _client;
        private static bool _myTurn = false;
        private static int[,] _gameBoard = new int[3, 3];

        static void Main(string[] args)
        {
            // get ip address to connect to
            string connectAddress = "127.0.0.1";
            int connectPort = 3333;

            // if two parameters were passed in, handle it as an address and port combo
            // if only one parameter was passed in, handle it as just the port
            if (args.Length == 2)
            {
                connectAddress = args[0];
                int.TryParse(args[1], out connectPort);
            }
            else if (args.Length == 1)
            {
                int.TryParse(args[0], out connectPort);
            }

            try
            {
                // connect to server
                _client = new TcpClient(connectAddress, connectPort);
            }
            catch (Exception e)
            {
                Console.WriteLine("Could not create client: {0}", e.Message);
                Environment.Exit(1);
            }

            if (_client.Connected)
            {
                Console.WriteLine("Successfully connected to server!");
            }
            else
            {
                Console.WriteLine("Failed to connect to server {0}:{1}", connectAddress, connectPort);
            }

            var stream = _client.GetStream();
            while (_client.Client.Connected)
            {
                byte[] buffer = new byte[1024];
                int received = 0;

                if (_myTurn)
                {
                    Console.Write("Enter command: ");
                    string command = Console.ReadLine();
                    var message = Encoding.UTF8.GetBytes(command);

                    try
                    {
                        stream.Write(message, 0, message.Length);
                    }
                    catch (Exception)
                    {
                        // ignore
                    }
                }

                // get data from server
                try
                {
                    //_client.Client.Receive(buffer, buffer.Length, SocketFlags.None);
                    received = stream.Read(buffer, 0, buffer.Length);
                }
                catch (Exception)
                {
                    // ignore exception
                }

                string serverResponse = Encoding.UTF8.GetString(buffer, 0, received);
                Console.WriteLine(serverResponse);

                if (serverResponse.StartsWith("CHAT"))
                {
                    serverResponse = serverResponse.Remove(0, 5);
                    Console.WriteLine(serverResponse);
                }
                else if (serverResponse.StartsWith("BOARD_UPDATE"))
                {
                    string[] s = serverResponse.Split('|');

                    string boardSize = s[1];
                    string gameState = s[2];
                    string currentPlayer = s[3];

                    Console.WriteLine("Updated game board state");
                    Console.WriteLine("Game board size: {0}", boardSize);
                    Console.WriteLine("Game state: {0}", gameState);
                    Console.WriteLine("Current player: {0}", currentPlayer);
                    Console.WriteLine();

                    if (currentPlayer == "1")
                    {
                        _myTurn = true;
                    }
                    else
                    {
                        _myTurn = false;
                    }

                    int w, h;
                    int.TryParse(boardSize.Substring(0, boardSize.IndexOf('x')), out w);
                    int.TryParse(
                        boardSize.Substring(boardSize.IndexOf('x') + 1,
                                            boardSize.Length - boardSize.IndexOf('x') - 1), out h);

                    if (gameState.Length != w*h)
                    {
                        Console.WriteLine("Corrupt game state");
                    }
                    else
                    {
                        for (int i = 0; i < w; i++)
                        {
                            for (int j = 0; j < h; j++)
                            {
                                int.TryParse(gameState[0].ToString(), out _gameBoard[j, i]);
                                gameState = gameState.Remove(0, 1);
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine(serverResponse);
                }
            }

            Console.WriteLine("Lost connection to server.");
        }
    }
}
