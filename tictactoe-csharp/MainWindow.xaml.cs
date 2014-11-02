using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using Microsoft.VisualBasic;

namespace tictactoe_csharp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private static TcpClient _client;

        public MainWindow()
        {
            InitializeComponent();

            // get server address
            string connectAddress = Interaction.InputBox(Properties.Resources.ipPrompt);

            // get name
            string name = Interaction.InputBox(Properties.Resources.namePrompt);

            // get port
            const int connectPort = 3333;

            // try to connect to server
            try
            {
                _client = new TcpClient(connectAddress, connectPort);
            }
            catch (Exception e)
            {
                MessageBox.Show(String.Format(Properties.Resources.clientCreationError, e.Message));
                Environment.Exit(1);
            }

            // display error message if server connection failed
            if (!_client.Connected)
            {
                MessageBox.Show(String.Format(Properties.Resources.serverConnectionError,
                                              connectAddress, connectPort));
            }

            // send player's name to server
            var message = Encoding.UTF8.GetBytes(String.Format("SET_NAME|{0}", name));
            _client.GetStream().Write(message, 0, message.Length);

            // start a new background thread to communicate with server
            Thread serverThread = new Thread(HandleServer);
            serverThread.IsBackground = true;
            serverThread.Start();
        }

        private void HandleServer()
        {
            // loop while we still have a connection to the server
            while (_client.Connected)
            {
                // read message from server
                byte[] buffer = new byte[1024];
                int received = 0;

                try
                {
                    received = _client.GetStream().Read(buffer, 0, 1024);
                }
                catch (Exception)
                {
                }

                // convert server's response into a string
                string response = Encoding.UTF8.GetString(buffer, 0, received);

                // each command is separated by '`' to allows us to handle each command separately in the case
                // that a bunch of messages get queued and read at the same time
                string[] commands = response.Split('`');

                for (int i = 0; i < commands.Length; i++)
                {
                    string command = commands[i];

                    if (command.StartsWith("CHAT|"))
                    {
                        // add chat message to chat box
                        command = command.Remove(0, "CHAT|".Length);
                        textBox1.Dispatcher.Invoke(new UpdateChatCallback(UpdateChat), new object[] {command});
                    }
                    else if (command.StartsWith("MARK|"))
                    {
                        // mark a button on the grid
                        command = command.Remove(0, "MARK|".Length);
                        grid1.Dispatcher.Invoke(new UpdatePlayFieldCallback(UpdatePlayField), new object[] {command});
                    }
                    else if (command.StartsWith("WINNER|"))
                    {
                        // notify the player if they've won or lost
                        command = command.Remove(0, "WINNER|".Length);

                        if (command == "1")
                        {
                            MessageBox.Show("You win!");
                        }
                        else if (command == "0")
                        {
                            MessageBox.Show("You lose.");
                        }
                    }
                }
            }

            // loop ended, no longer connected to server
            MessageBox.Show("Lost connection to server.");
        }

        public delegate void UpdateChatCallback(string message);
        private void UpdateChat(string message)
        {
            // append text to chat box
            textBox1.Text += message + Environment.NewLine;
            textBox1.ScrollToEnd();
        }

        public delegate void UpdatePlayFieldCallback(string message);
        private void UpdatePlayField(string message)
        {
            // each position is separated by a |, so get a list of all markers by splitting at the '|'s
            string[] marker = message.Split('|');

            for (int i = 0; i < marker.Length; i++)
            {
                // update the text on all buttons on the field
                string c = marker[i];

                if (i == 0) button1.Content = c;
                else if (i == 1) button2.Content = c;
                else if (i == 2) button3.Content = c;

                else if (i == 3) button4.Content = c;
                else if (i == 4) button5.Content = c;
                else if (i == 5) button6.Content = c;

                else if (i == 6) button7.Content = c;
                else if (i == 7) button8.Content = c;
                else if (i == 8) button9.Content = c;
            }
        }

        private void SetMarker(int y, int x)
        {
            // send marker request to server
            byte[] message = Encoding.UTF8.GetBytes(String.Format("MARK|{0}|{1}", x, y));
            _client.GetStream().Write(message, 0, message.Length);
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            SetMarker(0, 0);
        }

        private void button2_Click(object sender, RoutedEventArgs e)
        {
            SetMarker(0, 1);
        }

        private void button3_Click(object sender, RoutedEventArgs e)
        {
            SetMarker(0, 2);
        }

        private void button4_Click(object sender, RoutedEventArgs e)
        {
            SetMarker(1, 0);
        }

        private void button5_Click(object sender, RoutedEventArgs e)
        {
            SetMarker(1, 1);
        }

        private void button6_Click(object sender, RoutedEventArgs e)
        {
            SetMarker(1, 2);
        }

        private void button7_Click(object sender, RoutedEventArgs e)
        {
            SetMarker(2, 0);
        }

        private void button8_Click(object sender, RoutedEventArgs e)
        {
            SetMarker(2, 1);
        }

        private void button9_Click(object sender, RoutedEventArgs e)
        {
            SetMarker(2, 2);
        }

        private void textBox2_KeyDown(object sender, KeyEventArgs e)
        {
            // check if the player sent a chat message
            if (e.Key == Key.Return)
            {
                // don't send blank messages
                if (String.IsNullOrWhiteSpace(textBox2.Text))
                    return;

                // convert the message to a chat command and then send it to the server
                byte[] message = Encoding.UTF8.GetBytes(String.Format("CHAT|{0}", textBox2.Text));

                try
                {
                    _client.GetStream().Write(message, 0, message.Length);
                }
                catch (Exception)
                {
                }

                // clear chat input field
                textBox2.Text = "";
            }
        }
    }
}
