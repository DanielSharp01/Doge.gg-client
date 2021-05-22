using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Doge.gg_client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        JsonWebsocket socket = new JsonWebsocket(new Uri("ws://localhost:5050"));
        public TaskBarClickCommand TaskBarClickCommand { get; }
        private bool forRealClose;

        public MainWindow()
        {
            TaskBarClickCommand = new TaskBarClickCommand(this);
            DataContext = this;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            
            socket.ConnectionStatusChanged += (status) =>
            {
                dogeGGIndicator.Fill = status ? Brushes.LimeGreen : Brushes.Red;
            };
            var gameClient = new GameClient();
#if CHARM_BOT
            processMemoryIndicator.Fill = Brushes.Red;
            await gameClient.GetChampions();
#endif
            gameClient.ClientStatusChanged += (status) =>
            {
                leagueClientIndicator.Fill = status ? Brushes.LimeGreen : Brushes.Red;
            };
            gameClient.MemoryReaderStatusChanged += (status) =>
            {
                processMemoryIndicator.Fill = status ? Brushes.LimeGreen : Brushes.Red;
            };
            gameClient.ClientConnected += async (players, activePlayerName) => await socket?.Send(new JObject
            {
                ["type"] = "clientConnected",
                ["players"] = new JArray(players),
                ["activePlayerName"] = activePlayerName,
            });
            gameClient.ClientDisconnected += async () => await socket?.Send(new JObject
            {
                ["type"] = "clientDisconnected",
            });
            gameClient.GotEvents += async (events) => await socket?.Send(new JObject
            {
                ["type"] = "gotEvents",
                ["events"] = new JArray(events),
            });
            _ = gameClient.Observe();
            socket.RecievedMessage += obj =>
            {
                if (obj?["type"].ToString() == "connected")
                {
                    gameClient.ReemitRunning();
                }
            };
            _ = socket.Connect();
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
            socket.Disconnect();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (forRealClose) return;
            Hide();
            e.Cancel = true;
        }

        private void Open_Clicked(object sender, RoutedEventArgs e)
        {
            TaskBarClickCommand.Execute(null);
        }

        private void Close_Clicked(object sender, RoutedEventArgs e)
        {
            forRealClose = true;
            Close();
        }
    }

    public class TaskBarClickCommand : ICommand
    {
        Window window;
        public TaskBarClickCommand(Window window)
        {
            this.window = window;
        }
        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            window.Show();
        }
    }
}
