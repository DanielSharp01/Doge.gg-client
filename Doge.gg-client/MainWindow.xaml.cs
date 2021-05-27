using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
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
        readonly Config config;
        readonly JsonWebsocket socket;
        public TaskBarClickCommand TaskBarClickCommand { get; }
        private CancellationTokenSource reopenToken = null;
        private const string guid = "13c6835a-74c3-4c04-b7c4-fe11499e6bf4";

        public MainWindow()
        {
            config = new Config();
            socket = new JsonWebsocket(config);
            TaskBarClickCommand = new TaskBarClickCommand(this);
            DataContext = this;

            using (var client = new NamedPipeClientStream(".", guid, PipeDirection.Out))
            {
                try
                {
                    client.Connect(100);
                    Environment.Exit(0);
                }
                catch (TimeoutException)
                {
                    _ = AwaitForReopen();
                }
            }
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
            reopenToken.Cancel();
        }

        private void Open_Clicked(object sender, RoutedEventArgs e)
        {
            TaskBarClickCommand.Execute(null);
        }

        private void Close_Clicked(object sender, RoutedEventArgs e)
        {
            reopenToken.Cancel();
            Close();
        }

        private async Task AwaitForReopen()
        {
            reopenToken = new CancellationTokenSource();
            using (NamedPipeServerStream pipeServer = new NamedPipeServerStream(guid, PipeDirection.In, 1, PipeTransmissionMode.Message, PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous))
            {
                await pipeServer.WaitForConnectionAsync(reopenToken.Token);
                ShowNormalize();
            }
            if (!reopenToken.IsCancellationRequested) _ = AwaitForReopen();
        }

        public void ShowNormalize()
        {
            WindowState = WindowState.Normal;
            Show();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
                Hide();
            }
        }
    }

    public class TaskBarClickCommand : ICommand
    {
        MainWindow window;
        public TaskBarClickCommand(MainWindow window)
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
            window.ShowNormalize();
        }
    }
}
