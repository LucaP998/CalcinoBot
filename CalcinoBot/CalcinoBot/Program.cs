using Telegram.Bot;
using Telegram.Bot.Types;
using System;
using System.Text.Json;
using File = System.IO.File;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace CalcinoBot
{
    public static class TelegramBot
    {
        static int SW_HIDE = 0;
        static int SW_SHOW = 5;
        static int SW_MIN = 2;
        // messages are stored even if the bot is offline, and when it goes online it answers them all at once. To prevent this, the stopwatched is used to ignore messages in the first 5 seconds of uptime.
        public static Stopwatch stopwatch;
        public static string apiKey = "insert telegram bot api key here";
        public static string jsonPath = @"insert path to json here";
        public static TelegramBotClient bot;
        public static string jsonString = "";
        public static Dictionary<string, string> players;
        public static string commandList = "/list : mostra tutti i giocatori attualmente inseriti nella lista.\n\n" +
                    "/add : aggiunge un giocatore alla lista e ne assegna una categoria. Le categorie accettate nel comando sono solo \"forte\" e \"debole\". Esempio: /add luca debole\n\n" +
                    "/remove : rimuove un giocatore dalla lista. Esempio: /remove luca\n\n" +
                    "/create : crea team basandosi sull'attuale lista di giocatori. Se il numero di giocatori forti e deboli non è lo stesso, verrà fatto presente quale giocatore è rimasto fuori (ancora non ho pensato a una soluzione migliore).\n\n" +
                    "/random : crea team casuali senza guardare la categoria assegnata a ciascun giocatore.\n\n" +
                    "/help : mostra i comandi disponibili.\n\n";
        

        public static void Main(string[] args)
        {
            stopwatch = new Stopwatch();
            stopwatch.Start();
            var handle = GetConsoleWindow();
            ShowWindow(handle, SW_MIN);
            try
            {
                string jsonString = File.ReadAllText(jsonPath);
                Console.WriteLine("Telegram calcino bot is running, press any key to stop it.");
                players = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonString);
                bot = new TelegramBotClient(apiKey);
                bot.StartReceiving(HandleUpdateAsync, (a, v, c) => { return null; });
                Console.ReadLine();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                players = new Dictionary<string, string>();
                players.Add("luca", "debole");
                bot = new TelegramBotClient(apiKey);
                bot.StartReceiving(HandleUpdateAsync, (a, v, c) => { return null; });
                Console.ReadLine();
            }
        }


        private static async Task HandleUpdateAsync(ITelegramBotClient itelegramBotClient, Update update, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(update.Message.Text) || stopwatch.Elapsed.Seconds < 5) return;
            update.Message.Text = update.Message.Text.ToLower();
            if (update.Message.Text.Equals("/start"))
            {

                await bot.SendTextMessageAsync(update.Message.Chat.Id, "Ciao! L'obiettivo di questo bot è creare team equilibrati, dividendo i giocatori in due categorie: forti e deboli.\n" +
                    "Il programma accoppierà forti e deboli insieme.\n\n" +
                    "Questi sono i comandi che puoi usare in chat:\n\n" +
                    commandList

                    );

            }
            else if (update.Message.Text.Equals("/list"))
            {
                await bot.SendTextMessageAsync(update.Message.Chat.Id, PrintPlayerList());
            }
            else if (update.Message.Text.StartsWith("/add"))
            {
                var args = update.Message.Text.Split(' ');
                if (args.Length == 3)
                {
                    await bot.SendTextMessageAsync(update.Message.Chat.Id, AddPlayer(name: args[1], category: args[2]));
                }
                else
                {
                    await bot.SendTextMessageAsync(update.Message.Chat.Id, "Errore: il comando non è valido, la sintassi corretta è \"/add nome categoria\". \nEsempio: /add luca debole\nOppure: /add filippo forte");
                }
            }
            else if (update.Message.Text.StartsWith("/remove"))
            {
                var args = update.Message.Text.Split(' ');
                if (args.Length == 2)
                {
                    await bot.SendTextMessageAsync(update.Message.Chat.Id, RemovePlayer(name: args[1]));
                }
                else
                {
                    await bot.SendTextMessageAsync(update.Message.Chat.Id, "Errore: il comando non è valido, la sintassi corretta è \"/remove nome\". Esempio: /remove luca");
                }
            }
            else if (update.Message.Text.Equals("/create"))
            {
                await bot.SendTextMessageAsync(update.Message.Chat.Id, CreateBalancedTeams());
            }
            else if (update.Message.Text.Equals("/help"))
            {
                await bot.SendTextMessageAsync(update.Message.Chat.Id, commandList);
            }
            else if (update.Message.Text.Equals("/random"))
            {
                await bot.SendTextMessageAsync(update.Message.Chat.Id, CreateRandomTeams());
            }

        }


        private static string PrintPlayerList()
        {
            var playerList = "";
            int strongCounter = 0;
            int weakCounter = 0;
            foreach (var keyValuePair in players)
            {
                playerList += keyValuePair.Key + " - " + keyValuePair.Value + "\n";
                if (keyValuePair.Value == "forte")
                {
                    strongCounter++;
                }
                else
                {
                    weakCounter++;
                }
            }
            return playerList.Trim() + $"\n\nForti: {strongCounter}\nDeboli: {weakCounter}\nTotale: {strongCounter + weakCounter}";
        }

        private static string AddPlayer(string name, string category)
        {
            try
            {
                if (!category.Equals("forte") && !category.Equals("debole"))
                {
                    return $"Errore: la categoria {category} non è valida. Le categorie valide sono: forte, debole.";
                }
                players.Add(name, category);
                var newJsonString = JsonSerializer.Serialize(players, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(jsonPath, newJsonString);
                return PrintPlayerList();
            }
            catch (Exception e)
            {
                return "Errore: non è stato possibile aggiungere il giocatore.";
            }
        }

        private static string RemovePlayer(string name)
        {
            try
            {
                if (!players.ContainsKey(name)) throw new Exception("");
                players.Remove(name);
                var newJsonString = JsonSerializer.Serialize(players, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(jsonPath, newJsonString);
                return PrintPlayerList();
            }
            catch (Exception e)
            {
                return "Errore: non è stato possibile rimuovere il giocatore";
            }
        }

        private static string CreateBalancedTeams()
        {
            try
            {
                if (players.Count <= 0)
                {
                    throw new Exception("errore");
                }
                List<string> strongPlayers = new List<string>();
                List<string> weakPlayers = new List<string>();
                List<string[]> teams = new List<string[]>();
                foreach (var keyValuePair in players)
                {
                    if (keyValuePair.Value.Equals("forte"))
                    {
                        strongPlayers.Add(keyValuePair.Key);
                    }
                    else
                    {
                        weakPlayers.Add(keyValuePair.Key);
                    }
                }
                while (strongPlayers.Count > 0 && weakPlayers.Count > 0)
                {
                    Random random = new Random();
                    int strongPlayerIndex = random.Next(strongPlayers.Count);
                    int weakPlayerIndex = random.Next(weakPlayers.Count);
                    teams.Add(new[] { strongPlayers[strongPlayerIndex], weakPlayers[weakPlayerIndex] });
                    strongPlayers.RemoveAt(strongPlayerIndex);
                    weakPlayers.RemoveAt(weakPlayerIndex);
                }
                var message = "";
                for (int i = 0; i < teams.Count; i++)
                {
                    string[]? team = teams[i];
                    message += $"Team {i + 1}: {team[0]} - {team[1]}\n";
                }
                if (strongPlayers.Count > 0)
                {
                    message += "\nSenza team: ";
                    foreach (string player in strongPlayers)
                    {
                        message += player + " ";
                    }
                }
                if (weakPlayers.Count > 0)
                {
                    message += "\nSenza team: ";
                    foreach (string player in weakPlayers)
                    {
                        message += player + " ";
                    }
                }
                message = message.Trim();
                return message;
            }
            catch (Exception e)
            {
                return "Errore: non è stato possibile creare i team";
            }

        }

        private static string CreateRandomTeams()
        {
            try
            {
                if (players.Count <= 0)
                {
                    throw new Exception("errore");
                }
                string message = "";
                var playerNames = new List<string>();
                var teams = new List<List<string>>();
                foreach (var keyValuePair in players)
                {
                    playerNames.Add(keyValuePair.Key);
                }
                Random random = new Random();
                while (playerNames.Count >= 2)
                {
                    var team = new List<string>();

                    int firstMemberIndex = random.Next(playerNames.Count);
                    team.Add(playerNames[firstMemberIndex]);
                    playerNames.RemoveAt(firstMemberIndex);

                    int secondMemberIndex = random.Next(playerNames.Count);
                    team.Add(playerNames[secondMemberIndex]);
                    playerNames.RemoveAt(secondMemberIndex);

                    teams.Add(team);
                }
                for (int i = 0; i < teams.Count; i++)
                {
                    List<string>? team = teams[i];
                    message += $"Team {i+1}: {team[0]} - {team[1]}\n";
                }

                if (playerNames.Count >= 1)
                {
                    message += $"\nSenza team: {playerNames[0]}\n";
                }
                return message;
            }
            catch(Exception e)
            {
                return "Errore: non è stato possibile creare i team";
            }
        }


        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    }
}