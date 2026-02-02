using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TehGM.Wolfringo;
using TehGM.Wolfringo.Commands;
using TehGM.Wolfringo.Commands.Attributes;
using TehGM.Wolfringo.Hosting;
using TehGM.Wolfringo.Messages;
using TehGM.Wolfringo.Utilities;
using System.Timers;
using System.Text;

namespace SpyBot
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = new HostBuilder()
                .AddWolfringo(clientBuilder =>
                {
                    clientBuilder
                        .WithLogin("scodoublet@yahoo.com", "12345", LoginType.Email)
                        .WithDevice(WolfDevice.Other)
                        .WithVersion(new Version(4, 0, 0));
                })
                .ConfigureServices(services =>
                {
                    services.AddSingleton<GameService>();
                    services.AddCommands();
                })
                .Build();

            await host.RunAsync();
        }
    }

    public class GameState
    {
        public bool IsActive { get; set; }
        public bool IsEnglish { get; set; }
        public uint CreatorID { get; set; }
        public List<Player> Players { get; set; } = new List<Player>();
        public Player Spy { get; set; }
        public string SecretWord { get; set; }
        public Dictionary<uint, int> Votes { get; set; } = new Dictionary<uint, int>();
        public DateTime StartTime { get; set; }
        public Timer IdleTimer { get; set; }
        public Timer VoteTimer { get; set; }
        public Timer JoinTimer { get; set; }
    }

    public class Player
    {
        public uint ID { get; set; }
        public string Name { get; set; }
        public int Points { get; set; } = 0;
    }

    public class GameService
    {
        private readonly Dictionary<long, GameState> _groupGames = new Dictionary<long, GameState>();
        private readonly Dictionary<uint, int> _globalPoints = new Dictionary<uint, int>();
        private readonly Dictionary<long, Dictionary<uint, int>> _groupPoints = new Dictionary<long, Dictionary<uint, int>>();
        private readonly string[] _fruits = new string[]
        {
            "تفاح", "برتقال", "موز", "فراولة", "عنب", "كرز", "بطيخ", "مانجو", "أناناس", "خوخ",
            "تين", "رمان", "جوافة", "كمثرى", "ليمون", "يوسفي", "مشمش", "شمام", "توت بري", "جوز الهند",
            "أفوكادو", "برقوق", "فاكهة العاطفة", "توت", "لايمون", "توت العليق الأسود", "توت أزرق", "جوز دراق", "بابايا", "كيوي",
            "ليمون أخضر", "تمر", "إجاص", "نارنج", "جريب فروت", "توت العليق", "توت أسود", "دوريان", "فاكهة التنين", "جاك فروت",
            "رامبوتان", "ليتشي", "كارامبولا", "سالاك", "كاكاو", "سفارجل", "بندق", "لوز", "كستناء", "حوز",
            "صنوبر", "أكاي", "أسيرولا", "أكي", "مانجو أفريقي", "أكيبي", "فراولة جبال الألب", "أماناتسو", "أمارا", "أمباريلا",
            "تفاح أمبروزيا", "شمام أمبروزيا", "أملا", "أناناتو", "أنونا", "تفاحة أمريكية", "مايابل", "أرونيا", "باذنجان أفريقي", "باشن فروت",
            "بيل بيري", "بيغني", "بيلبيري", "بيلمبي", "بلاك أبل", "بلاك تشيري", "بلاك كورانت", "بلاك مولبيري", "بلاك راسبيري", "بلاك سابوت",
            "بلاكبيري", "بلود أورانج", "بلو باسيون فروت", "بلو بيري", "بريد فروت", "بروش تشيري", "بوذا هاند", "بورديكين بلوم", "بوشيل أند بيري", "جيلي بين",
            "بلو بيري", "باتر فروت", "كاكاو", "كاكتوس بير", "كالاباش", "كالامانسي", "كامو كامو", "كانيستيل", "كانتالوب", "كيب غوسبيري",
            "كارا كارا", "كرامبولا", "كاريسا", "كاسكارا", "كاشو أبل", "كاتمون", "كافيار لايم", "سيدار باي تشيري", "سيمليديك", "تشيمبيداك",
            "سييلون غوسبيري", "چاريتتشويلو", "تشايوتي", "تشيريمويا", "تشيري بلوم", "تشيكو فروت", "تشوكولات فروت", "تشوكبيري", "تشوكتشيري", "سيترون",
            "كليمنتين", "كلودبيري", "كلستر فيغ", "كوكي أبل", "كوكو دي مير", "كوكو بلوم", "كوكونات", "كوفي تشيري", "كورنيليان تشيري", "كراب أبل",
            "كرانبيري", "كروبيري", "كوكومبر", "كومكوات", "كوبواكو", "كورانت", "كاستارد أبل", "داباي", "دامسون", "دامسون بلوم",
            "دانغل بيري", "دارلينغ بلوم", "ديت", "ديت بلوم", "دافيدسونز بلوم", "ديد مانز فينغرز", "ديكايزنيا", "دوبل كوكونات", "دراكونتو ميلون", "دراغون فروت",
            "دوكو", "دوريان"
        };
        private readonly Random _random = new Random();

        public GameState GetGame(long groupID)
        {
            _groupGames.TryGetValue(groupID, out var game);
            return game;
        }

        public void StartGame(long groupID, uint creatorID, bool isEnglish)
        {
            var game = new GameState
            {
                IsActive = true,
                IsEnglish = isEnglish,
                CreatorID = creatorID,
                StartTime = DateTime.Now
            };
            _groupGames[groupID] = game;

            game.JoinTimer = new Timer(180000); // 3 minutes
            game.JoinTimer.Elapsed += (s, e) => CloseGameIfIdle(groupID);
            game.JoinTimer.Start();
        }

        public void CloseGame(long groupID)
        {
            if (_groupGames.TryGetValue(groupID, out var game))
            {
                game.IsActive = false;
                game.JoinTimer?.Stop();
                game.IdleTimer?.Stop();
                game.VoteTimer?.Stop();
                _groupGames.Remove(groupID);
            }
        }

        private void CloseGameIfIdle(long groupID)
        {
            if (_groupGames.TryGetValue(groupID, out var game))
            {
                if (game.Players.Count == 0 || !game.Players.Any(p => p.ID != game.CreatorID))
                {
                    CloseGame(groupID);
                }
            }
        }

        public string GetRandomFruit()
        {
            return _fruits[_random.Next(_fruits.Length)];
        }

        public Player GetPlayer(long groupID, uint userID)
        {
            var game = GetGame(groupID);
            return game?.Players.FirstOrDefault(p => p.ID == userID);
        }

        public void AddPlayer(long groupID, uint userID, string name)
        {
            var game = GetGame(groupID);
            if (game != null && !game.Players.Any(p => p.ID == userID))
            {
                game.Players.Add(new Player { ID = userID, Name = name });
                game.JoinTimer?.Stop(); // reset if needed
            }
        }

        public void RemovePlayer(long groupID, uint userID)
        {
            var game = GetGame(groupID);
            if (game != null)
            {
                game.Players.RemoveAll(p => p.ID == userID);
            }
        }

        public void StartVoting(long groupID)
        {
            var game = GetGame(groupID);
            if (game != null)
            {
                game.SecretWord = GetRandomFruit();
                game.Spy = game.Players[_random.Next(game.Players.Count)];
                game.Votes.Clear();

                game.VoteTimer = new Timer(180000); // 3 min for voting
                game.VoteTimer.Elapsed += (s, e) => HandleVoteTimeout(groupID);
                game.VoteTimer.Start();

                game.IdleTimer = new Timer(120000); // 2 min for remaining
                game.IdleTimer.Elapsed += (s, e) => HandleVoteTimeout(groupID);
            }
        }

        public void AddVote(long groupID, uint voterID, int vote)
        {
            var game = GetGame(groupID);
            if (game != null && game.Players.Any(p => p.ID == voterID))
            {
                game.Votes[voterID] = vote;
                if (game.Votes.Count == game.Players.Count)
                {
                    EndRound(groupID);
                }
            }
        }

        private void HandleVoteTimeout(long groupID)
        {
            var game = GetGame(groupID);
            if (game != null)
            {
                var nonVoters = game.Players.Where(p => !game.Votes.ContainsKey(p.ID)).ToList();
                foreach (var nv in nonVoters)
                {
                    RemovePlayer(groupID, nv.ID);
                }
                if (game.Players.Count > 1)
                {
                    EndRound(groupID);
                }
                else
                {
                    CloseGame(groupID);
                }
            }
        }

        private void EndRound(long groupID)
        {
            var game = GetGame(groupID);
            if (game != null)
            {
                game.VoteTimer?.Stop();
                game.IdleTimer?.Stop();

                // calculate points
                var spyID = game.Spy.ID;
                var groupPoints = GetGroupPoints(groupID);
                int spyPointsChange = 0;
                foreach (var vote in game.Votes)
                {
                    var votedPlayer = game.Players.FirstOrDefault(p => GetPlayerIndex(game, p) == vote.Value);
                    if (votedPlayer?.ID == spyID)
                    {
                        UpdatePoints(groupID, vote.Key, 1);
                        spyPointsChange -= 1;
                    }
                }
                UpdatePoints(groupID, spyID, spyPointsChange);

                // ask to continue
                game.StartTime = DateTime.Now; // reset for continue
            }
        }

        private int GetPlayerIndex(GameState game, Player player)
        {
            return game.Players.IndexOf(player) + 1;
        }

        private void UpdatePoints(long groupID, uint userID, int change)
        {
            if (!_globalPoints.ContainsKey(userID))
                _globalPoints[userID] = 0;
            _globalPoints[userID] += change;

            var groupPoints = GetGroupPoints(groupID);
            if (!groupPoints.ContainsKey(userID))
                groupPoints[userID] = 0;
            groupPoints[userID] += change;
        }

        private Dictionary<uint, int> GetGroupPoints(long groupID)
        {
            if (!_groupPoints.ContainsKey(groupID))
                _groupPoints[groupID] = new Dictionary<uint, int>();
            return _groupPoints[groupID];
        }

        public List<KeyValuePair<uint, int>> GetGroupRanking(long groupID)
        {
            var groupPoints = GetGroupPoints(groupID);
            return groupPoints.OrderByDescending(kv => kv.Value).Take(10).ToList();
        }

        public int GetGlobalRank(uint userID)
        {
            var sorted = _globalPoints.OrderByDescending(kv => kv.Value).ToList();
            return sorted.FindIndex(kv => kv.Key == userID) + 1;
        }

        public int GetTotalPoints(uint userID)
        {
            _globalPoints.TryGetValue(userID, out int points);
            return points;
        }
    }

    [Command("جاسوس")]
    [Command("جس")]
    [Command("spy")]
    public class SpyCommands : CommandHandlerBase
    {
        private readonly GameService _gameService;
        private readonly IWolfClient _client;

        public SpyCommands(GameService gameService, IWolfClient client)
        {
            _gameService = gameService;
            _client = client;
        }

        [Command("جديد")]
        [Command("new")]
        public async Task NewGameAsync(CommandContext ctx)
        {
            if (ctx.Message is not ChatMessage msg || msg.IsGroupMessage == false)
                return;

            long groupID = msg.RecipientID;
            var game = _gameService.GetGame(groupID);
            if (game?.IsActive == true)
            {
                await ctx.ReplyTextAsync("اللعبة جارية بالفعل.");
                return;
            }

            bool isEnglish = ctx.Command == "spy" && ctx.Arguments[0] == "new";
            _gameService.StartGame(groupID, msg.SenderID.Value, isEnglish);

            string joinMsg = isEnglish ?
                "/me Come on, sweeties, we've started the game. Join the game with this command: \"!spy join\"" :
                "/me يلا يا حلوين بدينا اللعبه انظموا للعبه بالأمر هذا \"!جاسوس انظم او !جس انظم\"";
            await ctx.ReplyTextAsync(joinMsg);
        }

        [Command("انظم")]
        [Command("join")]
        public async Task JoinGameAsync(CommandContext ctx)
        {
            if (ctx.Message is not ChatMessage msg || msg.IsGroupMessage == false)
                return;

            long groupID = msg.RecipientID;
            var game = _gameService.GetGame(groupID);
            if (game?.IsActive != true)
            {
                await ctx.ReplyTextAsync("لا توجد لعبة جارية.");
                return;
            }

            var profile = await _client.GetUserProfile(msg.SenderID.Value);
            _gameService.AddPlayer(groupID, msg.SenderID.Value, profile.Name ?? "Unknown");

            await ctx.ReplyTextAsync($"/me انضم {profile.Name} إلى اللعبة.");
        }

        [Command("بدء")]
        [Command("start")]
        public async Task StartGameAsync(CommandContext ctx)
        {
            if (ctx.Message is not ChatMessage msg || msg.IsGroupMessage == false)
                return;

            long groupID = msg.RecipientID;
            var game = _gameService.GetGame(groupID);
            if (game?.IsActive != true || game.CreatorID != msg.SenderID.Value)
            {
                await ctx.ReplyTextAsync("فقط المنشئ يمكنه بدء اللعبة.");
                return;
            }

            if (game.Players.Count < 2)
            {
                await ctx.ReplyTextAsync("يجب أن يكون هناك على الأقل لاعبان.");
                return;
            }

            _gameService.StartVoting(groupID);

            // send player list
            StringBuilder list = new StringBuilder("قائمة اللاعبين:\n");
            for (int i = 0; i < game.Players.Count; i++)
            {
                list.AppendLine($"{i+1} - {game.Players[i].Name} (ID: {game.Players[i].ID})");
            }
            await ctx.ReplyTextAsync(list.ToString());

            // send secret to all except spy
            foreach (var player in game.Players)
            {
                if (player.ID == game.Spy.ID)
                {
                    string spyMsg = game.IsEnglish ?
                        "/alert You are the spy, my heart, deceive them and choose any player from the list so no one suspects you 🥴" :
                        "/alert انت الجاسوس يا قلب قلبي اخدعهم واختار أي لاعب من القائمه عشان محد يشك فيك 🥴";
                    await _client.SendMessageAsync(new PrivateMessage(player.ID, spyMsg));
                }
                else
                {
                    string secretMsg = game.IsEnglish ?
                        $"The secret word is: {game.SecretWord}" :
                        $"كلمة السر هي: {game.SecretWord}";
                    await _client.SendMessageAsync(new PrivateMessage(player.ID, secretMsg));
                }
            }
        }

        [Command("طرد")]
        [Command("kick")]
        public async Task KickPlayerAsync(CommandContext ctx)
        {
            if (ctx.Message is not ChatMessage msg || msg.IsGroupMessage == false || ctx.Arguments.Length < 1)
                return;

            long groupID = msg.RecipientID;
            var game = _gameService.GetGame(groupID);
            if (game?.IsActive != true || game.CreatorID != msg.SenderID.Value)
                return;

            if (int.TryParse(ctx.Arguments[0], out int index) && index > 0 && index <= game.Players.Count)
            {
                var player = game.Players[index - 1];
                _gameService.RemovePlayer(groupID, player.ID);
                await ctx.ReplyTextAsync($"/me تم طرد {player.Name}.");
            }
        }

        [Command("ترتيب")]
        [Command("rank")]
        [Command("at")]
        [Command("arrangement")]
        public async Task ShowGroupRankingAsync(CommandContext ctx)
        {
            if (ctx.Message is not ChatMessage msg || msg.IsGroupMessage == false)
                return;

            long groupID = msg.RecipientID;
            var ranking = _gameService.GetGroupRanking(groupID);

            StringBuilder sb = new StringBuilder("ترتيب اللاعبين في القناة:\n");
            for (int i = 0; i < ranking.Count; i++)
            {
                var kv = ranking[i];
                var profile = await _client.GetUserProfile(kv.Key); // assuming cache or fetch
                string name = profile?.Name ?? "Unknown";
                sb.AppendLine($"[{i+1}] - [ID: {kv.Key}] + [{name}] + [مجموع النقاط: {kv.Value} نقطة]");
            }
            await ctx.ReplyTextAsync(sb.ToString());
        }

        [Command("عام")]
        [Command("general")]
        [Command("gl")]
        public async Task ShowGlobalRankAsync(CommandContext ctx)
        {
            if (ctx.Message is not ChatMessage msg)
                return;

            int rank = _gameService.GetGlobalRank(msg.SenderID.Value);
            await ctx.ReplyTextAsync($"ترتيبك العام: {rank}");
        }

        [Command("مجموع")]
        [Command("total")]
        public async Task ShowTotalPointsAsync(CommandContext ctx)
        {
            if (ctx.Message is not ChatMessage msg)
                return;

            int points = _gameService.GetTotalPoints(msg.SenderID.Value);
            await ctx.ReplyTextAsync($"مجموع نقاطك: {points}");
        }

        [Command("مساعده")]
        [Command("مساعدة")]
        [Command("help")]
        public async Task ShowHelpAsync(CommandContext ctx)
        {
            bool isEnglish = ctx.Command == "spy" && ctx.Arguments[0] == "help";
            string helpText = isEnglish ?
                @"/""!New Spy"", ""!Jess New"" to start a good game
/""Spy Join"", ""!Jess Join"" to join the game
/""Spy Start"", ""!Jess Start"" to start the game
/""Spy Kick (Player Number)"", ""!Jess Kick (Player Number)"" to kick a player from the game
/""Spy Rank"", ""!Jess Rank"" to show the player ranking in the channel
/""Spy General"", ""!Jess General"" to show the player ranking at the application level
/""Spy Help"", ""!Jess Help"" to access the help menu" :
                @"/""!جاسوس جديد"" ، ""!جس جديد"" لبدء لعبه جيد
/""جاسوس انظم"" ، ""!جس انظم"" للانظمام للعبه
/""!جاسوس بدء"" ، ""!جس بدء"" لبدء اللعبه
/""!جاسوس طرد(رقم اللاعب)"" ، ""!جس طرد(رقم اللاعب) لطرد لاعب من اللعبه
/""!جاسوس ترتيب"" ،""!جس ترتيب"" لعرض ترتيب اللاعبين في القناه
/""!جاسوس عام"" ، ""!جس عام"" لعرض ترتيب اللاعب على مستوى التطبيق
/""!جاسوس مساعده"" ، !جس مساعده"" لعرض قائمة المساعده";

            await ctx.ReplyTextAsync(helpText);
        }

        // Handle votes - since votes are direct numbers, handle in general message listener if needed
        // But for simplicity, assume commands handle, but votes are not commands, they are direct numbers.
        // So need to listen to all messages.
    }

    // To handle votes, add a message listener
    public class MessageListener : IBotMessageListener
    {
        private readonly GameService _gameService;
        private readonly IWolfClient _client;

        public MessageListener(GameService gameService, IWolfClient client)
        {
            _gameService = gameService;
            _client = client;
        }

        public async Task OnMessageReceivedAsync(IMessage message)
        {
            if (message is ChatMessage msg && msg.IsGroupMessage && msg.IsText && int.TryParse(msg.Text.Trim(), out int voteNumber))
            {
                long groupID = msg.RecipientID;
                var game = _gameService.GetGame(groupID);
                if (game?.IsActive == true && game.Votes.ContainsKey(msg.SenderID.Value) == false && game.Players.Any(p => p.ID == msg.SenderID.Value) && voteNumber > 0 && voteNumber <= game.Players.Count)
                {
                    _gameService.AddVote(groupID, msg.SenderID.Value, voteNumber);

                    if (game.Votes.Count == game.Players.Count)
                    {
                        // reveal spy
                        string revealMsg = game.IsEnglish ?
                            $"/alert This is the traitor: Here is the spy's ID number {game.Spy.ID} {game.Spy.Name}" :
                            $"/alert هذا هو الخاين البواق : هنا رقم عضوية الجاسوس ID {game.Spy.ID} اسم اللاعب او اسمه المستعار {game.Spy.Name}";
                        await _client.SendMessageAsync(new ChatMessage(msg.RecipientID, revealMsg, true));

                        // ask continue
                        string continueMsg = game.IsEnglish ?
                            "/alert If you want to continue the game, send the number 1, or if you don't want to, send the number 2." :
                            "/alert اذا ودك تكمل اللعبه ارسل رقم 1 او اذا مالك خاطر ارسل رقم 2";
                        await _client.SendMessageAsync(new ChatMessage(msg.RecipientID, continueMsg, true));
                    }
                }
                else if (game?.IsActive == true && game.Votes.Count == game.Players.Count && game.CreatorID == msg.SenderID.Value && (voteNumber == 1 || voteNumber == 2))
                {
                    if (voteNumber == 1)
                    {
                        // continue
                        _gameService.StartVoting(groupID);
                        // resend list, secret, etc. as in start
                        StringBuilder list = new StringBuilder("قائمة اللاعبين:\n");
                        for (int i = 0; i < game.Players.Count; i++)
                        {
                            list.AppendLine($"{i+1} - {game.Players[i].Name} (ID: {game.Players[i].ID})");
                        }
                        await _client.SendMessageAsync(new ChatMessage(msg.RecipientID, list.ToString(), true));

                        foreach (var player in game.Players)
                        {
                            if (player.ID == game.Spy.ID)
                            {
                                string spyMsg = game.IsEnglish ?
                                    "/alert You are the spy, my heart, deceive them and choose any player from the list so no one suspects you 🥴" :
                                    "/alert انت الجاسوس يا قلب قلبي اخدعهم واختار أي لاعب من القائمه عشان محد يشك فيك 🥴";
                                await _client.SendMessageAsync(new PrivateMessage(player.ID, spyMsg));
                            }
                            else
                            {
                                string secretMsg = game.IsEnglish ?
                                    $"The secret word is: {game.SecretWord}" :
                                    $"كلمة السر هي: {game.SecretWord}";
                                await _client.SendMessageAsync(new PrivateMessage(player.ID, secretMsg));
                            }
                        }
                    }
                    else
                    {
                        _gameService.CloseGame(groupID);
                        await _client.SendMessageAsync(new ChatMessage(msg.RecipientID, "/me اللعبة انتهت.", true));
                    }
                }
            }
        }
    }
}
