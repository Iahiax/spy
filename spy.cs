/*
 * Spy Game Bot for Wolf Live Platform
 * 
 * التثبيت والتشغيل:
 * 1. قم بتثبيت .NET SDK من: https://dotnet.microsoft.com/download
 * 2. قم بتثبيت الحزم المطلوبة:
 *    dotnet add package Wolfringo
 *    dotnet add package Wolfringo.Hosting
 * 3. قم بتشغيل البوت:
 *    dotnet run
 * 
 * الأوامر المتاحة:
 * - !جاسوس انشاء / !جس جديد / !spy new - لإنشاء لعبة جديدة
 * - !جاسوس انظم / !جس انظم / !spy join - للانضمام للعبة
 * - !جاسوس بدء / !جس بدء / !spy start - لبدء اللعبة
 * - !جاسوس طرد [رقم] / !جس طرد [رقم] / !spy kick [رقم] - لطرد لاعب
 * - !جاسوس ترتيب / !جس ترتيب / !spy arrangement - لعرض الترتيب في القناة
 * - !جاسوس عام / !جس عام / !spy general - لعرض الترتيب العام
 * - !جاسوس مجموع / !جس مجموع / !spy total - لعرض مجموع النقاط
 * - !جاسوس مساعده / !جس مساعده / !spy help - لعرض المساعدة
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TehGM.Wolfringo;
using TehGM.Wolfringo.Commands;
using TehGM.Wolfringo.Messages;
using TehGM.Wolfringo.Messages.Responses;

namespace SpyGameBot
{
    // ========== Main Program Class ==========
    public class Program
    {
        private static WolfClient _client;
        private static CommandsService _commandsService;

        public static async Task Main(string[] args)
        {
            Console.WriteLine("=== Spy Game Bot Starting ===");
            
            // Create Wolf client
            _client = new WolfClientBuilder()
                .WithAutoReconnection()
                .Build();

            // Create commands service
            _commandsService = new CommandsServiceBuilder(_client)
                .WithPrefix("!")
                .Build();

            // Connect events
            _client.Connected += OnConnected;
            _client.Disconnected += OnDisconnected;
            _client.ErrorOccurred += OnError;

            // Add command handler
            _commandsService.AddHandlers<SpyGameCommands>();

            // Connect to Wolf
            try
            {
                await _client.ConnectAsync();
                Console.WriteLine("Connecting to Wolf...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect: {ex.Message}");
                return;
            }

            // Wait indefinitely
            await Task.Delay(-1);
        }

        private static async void OnConnected(object sender, EventArgs e)
        {
            Console.WriteLine("✓ Connected to Wolf!");
            
            try
            {
                // Login
                await _client.LoginAsync("scodoublet@yahoo.com", "12345", WolfLoginType.Email);
                Console.WriteLine("✓ Logged in successfully!");

                // Subscribe to messages
                await _client.SubscribeAllMessagesAsync();
                Console.WriteLine("✓ Subscribed to messages!");

                // Start commands service
                await _commandsService.StartAsync();
                Console.WriteLine("✓ Commands service started!");
                Console.WriteLine("=== Bot is ready! ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during initialization: {ex.Message}");
            }
        }

        private static void OnDisconnected(object sender, EventArgs e)
        {
            Console.WriteLine("✗ Disconnected from Wolf!");
        }

        private static void OnError(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine($"Error occurred: {e.ExceptionObject}");
        }
    }

    // ========== Game Commands Handler ==========
    public class SpyGameCommands : CommandsHandlerBase
    {
        // Game storage per group
        private static Dictionary<uint, SpyGame> _activeGames = new Dictionary<uint, SpyGame>();
        
        // Player scores (global and per group)
        private static Dictionary<uint, int> _globalScores = new Dictionary<uint, int>();
        private static Dictionary<uint, Dictionary<uint, int>> _groupScores = new Dictionary<uint, Dictionary<uint, int>>();

        // Fruits list (كلمات السر)
        private static readonly string[] _fruits = new string[]
        {
            "تفاح", "برتقال", "موز", "فراولة", "عنب", "كرز", "بطيخ", "مانجو", "أناناس", "خوخ",
            "تين", "رمان", "جوافة", "كمثرى", "ليمون", "يوسفي", "مشمش", "شمام", "توت بري", "جوز الهند",
            "أفوكادو", "برقوق", "فاكهة العاطفة", "توت", "لايمون", "توت العليق الأسود", "توت أزرق",
            "جوز دراق", "بابايا", "كيوي", "ليمون أخضر", "تمر", "إجاص", "نارنج", "جريب فروت",
            "توت العليق", "توت أسود", "دوريان", "فاكهة التنين", "جاك فروت", "رامبوتان", "ليتشي",
            "كارامبولا", "سالاك", "كاكاو", "سفارجل", "بندق", "لوز", "كستناء", "حوز", "صنوبر"
        };

        private static Random _random = new Random();

        // ========== Create Game Commands ==========
        [Command("جاسوس انشاء")]
        [Command("جس جديد")]
        public async Task CreateGameArabic(CommandContext context)
        {
            await CreateGame(context, true);
        }

        [Command("spy new")]
        public async Task CreateGameEnglish(CommandContext context)
        {
            await CreateGame(context, false);
        }

        private async Task CreateGame(CommandContext context, bool isArabic)
        {
            if (context.Message.IsPrivateMessage)
            {
                await context.ReplyTextAsync(isArabic ? 
                    "لا يمكن إنشاء اللعبة في المحادثات الخاصة!" : 
                    "Cannot create game in private messages!");
                return;
            }

            uint groupId = context.Message.RecipientID.Value;

            if (_activeGames.ContainsKey(groupId))
            {
                await context.ReplyTextAsync(isArabic ?
                    "هناك لعبة نشطة بالفعل في هذه المجموعة!" :
                    "There's already an active game in this group!");
                return;
            }

            var game = new SpyGame
            {
                GroupId = groupId,
                CreatorId = context.Message.SenderID.Value,
                IsArabic = isArabic,
                State = GameState.WaitingForPlayers,
                CreatedAt = DateTime.UtcNow
            };

            _activeGames[groupId] = game;

            string message = isArabic ?
                "/me يلا يا حلوين بدينا اللعبه انظموا للعبه بالأمر هذا \"!جاسوس انظم او !جس انظم\"" :
                "/me Come on, sweeties, we've started the game. Join the game with this command: \"!spy join\"";

            await context.Client.SendGroupMessageAsync(groupId, message);

            // Start timeout timer (3 minutes)
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMinutes(3));
                if (_activeGames.TryGetValue(groupId, out var g) && g.State == GameState.WaitingForPlayers)
                {
                    _activeGames.Remove(groupId);
                    await context.Client.SendGroupMessageAsync(groupId, isArabic ?
                        "/alert تم إلغاء اللعبة بسبب انتهاء الوقت - لم ينضم أحد!" :
                        "/alert Game cancelled due to timeout - no one joined!");
                }
            });
        }

        // ========== Join Game Commands ==========
        [Command("جاسوس انظم")]
        [Command("جس انظم")]
        public async Task JoinGameArabic(CommandContext context)
        {
            await JoinGame(context);
        }

        [Command("spy join")]
        public async Task JoinGameEnglish(CommandContext context)
        {
            await JoinGame(context);
        }

        private async Task JoinGame(CommandContext context)
        {
            if (context.Message.IsPrivateMessage)
                return;

            uint groupId = context.Message.RecipientID.Value;
            uint playerId = context.Message.SenderID.Value;

            if (!_activeGames.TryGetValue(groupId, out var game))
            {
                return; // No active game
            }

            if (game.State != GameState.WaitingForPlayers)
            {
                await context.ReplyTextAsync(game.IsArabic ?
                    "اللعبة قد بدأت بالفعل!" :
                    "Game has already started!");
                return;
            }

            if (game.Players.Any(p => p.UserId == playerId))
            {
                await context.ReplyTextAsync(game.IsArabic ?
                    "أنت منضم بالفعل!" :
                    "You're already joined!");
                return;
            }

            // Get user info
            var user = await context.Client.GetUserAsync(playerId);
            game.Players.Add(new Player
            {
                UserId = playerId,
                Nickname = user?.Nickname ?? "Unknown"
            });

            await context.ReplyTextAsync(game.IsArabic ?
                $"✅ {user?.Nickname} انضم للعبة! ({game.Players.Count} لاعبين)" :
                $"✅ {user?.Nickname} joined the game! ({game.Players.Count} players)");
        }

        // ========== Start Game Commands ==========
        [Command("جاسوس بدء")]
        [Command("جس بدء")]
        public async Task StartGameArabic(CommandContext context)
        {
            await StartGame(context);
        }

        [Command("spy start")]
        public async Task StartGameEnglish(CommandContext context)
        {
            await StartGame(context);
        }

        private async Task StartGame(CommandContext context)
        {
            if (context.Message.IsPrivateMessage)
                return;

            uint groupId = context.Message.RecipientID.Value;
            uint userId = context.Message.SenderID.Value;

            if (!_activeGames.TryGetValue(groupId, out var game))
            {
                return;
            }

            if (game.CreatorId != userId)
            {
                await context.ReplyTextAsync(game.IsArabic ?
                    "فقط منشئ اللعبة يمكنه بدؤها!" :
                    "Only the game creator can start it!");
                return;
            }

            if (game.Players.Count < 3)
            {
                await context.ReplyTextAsync(game.IsArabic ?
                    "يجب أن يكون هناك 3 لاعبين على الأقل!" :
                    "Must have at least 3 players!");
                return;
            }

            if (game.State != GameState.WaitingForPlayers)
            {
                await context.ReplyTextAsync(game.IsArabic ?
                    "اللعبة قد بدأت بالفعل!" :
                    "Game already started!");
                return;
            }

            // Start the game
            game.State = GameState.Playing;
            game.SecretWord = _fruits[_random.Next(_fruits.Length)];
            game.SpyIndex = _random.Next(game.Players.Count);

            // Send player list
            string playerList = game.IsArabic ? "📋 قائمة اللاعبين:\n" : "📋 Players List:\n";
            for (int i = 0; i < game.Players.Count; i++)
            {
                var player = game.Players[i];
                playerList += $"{i + 1}. {player.Nickname} (ID: {player.UserId})\n";
            }

            await context.Client.SendGroupMessageAsync(groupId, playerList);

            // Send secret word to all players except spy
            for (int i = 0; i < game.Players.Count; i++)
            {
                var player = game.Players[i];
                try
                {
                    if (i == game.SpyIndex)
                    {
                        // Send spy message
                        string spyMsg = game.IsArabic ?
                            "/alert انت الجاسوس يا قلب قلبي اخدعهم واختار أي لاعب من القائمه عشان محد يشك فيك 🥴" :
                            "/alert You are the spy! Deceive them and choose any player from the list so no one suspects you 🥴";
                        await context.Client.SendPrivateMessageAsync(player.UserId, spyMsg);
                    }
                    else
                    {
                        // Send secret word
                        string wordMsg = game.IsArabic ?
                            $"/alert كلمة السر هي: {game.SecretWord}" :
                            $"/alert The secret word is: {game.SecretWord}";
                        await context.Client.SendPrivateMessageAsync(player.UserId, wordMsg);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to send PM to {player.UserId}: {ex.Message}");
                }
            }

            await context.Client.SendGroupMessageAsync(groupId, game.IsArabic ?
                "🎮 بدأت اللعبة! تم إرسال الرسائل للاعبين. اختاروا من تظنون أنه الجاسوس بإرسال رقم اللاعب!" :
                "🎮 Game started! Messages sent to players. Choose who you think is the spy by sending the player number!");

            // Start voting timeout (3 minutes)
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMinutes(3));
                if (_activeGames.TryGetValue(groupId, out var g) && g.State == GameState.Playing && g.Votes.Count == 0)
                {
                    _activeGames.Remove(groupId);
                    await context.Client.SendGroupMessageAsync(groupId, game.IsArabic ?
                        "/alert تم إلغاء اللعبة بسبب انتهاء الوقت!" :
                        "/alert Game cancelled due to timeout!");
                }
            });

            // Start partial voting timeout (2 minutes after first vote)
            _ = Task.Run(async () =>
            {
                while (_activeGames.TryGetValue(groupId, out var g) && g.State == GameState.Playing)
                {
                    await Task.Delay(TimeSpan.FromSeconds(10));
                    
                    if (g.Votes.Count > 0 && g.LastVoteTime.HasValue)
                    {
                        var timeSinceLastVote = DateTime.UtcNow - g.LastVoteTime.Value;
                        if (timeSinceLastVote > TimeSpan.FromMinutes(2) && g.Votes.Count < g.Players.Count)
                        {
                            // Remove players who didn't vote
                            var votedPlayerIds = g.Votes.Keys.ToList();
                            var playersToRemove = g.Players.Where(p => !votedPlayerIds.Contains(p.UserId)).ToList();
                            
                            foreach (var player in playersToRemove)
                            {
                                g.Players.Remove(player);
                            }

                            if (playersToRemove.Any())
                            {
                                await context.Client.SendGroupMessageAsync(groupId, game.IsArabic ?
                                    $"/alert تم طرد {playersToRemove.Count} لاعبين لعدم المشاركة!" :
                                    $"/alert Kicked {playersToRemove.Count} players for inactivity!");
                            }

                            await EndGame(context, groupId);
                            break;
                        }
                    }
                }
            });
        }

        // ========== Kick Player Commands ==========
        [Command("جاسوس طرد")]
        [Command("جس طرد")]
        public async Task KickPlayerArabic(CommandContext context, [MissingError("حدد رقم اللاعب!")] int playerNumber)
        {
            await KickPlayer(context, playerNumber);
        }

        [Command("spy kick")]
        public async Task KickPlayerEnglish(CommandContext context, [MissingError("Specify player number!")] int playerNumber)
        {
            await KickPlayer(context, playerNumber);
        }

        private async Task KickPlayer(CommandContext context, int playerNumber)
        {
            if (context.Message.IsPrivateMessage)
                return;

            uint groupId = context.Message.RecipientID.Value;
            uint userId = context.Message.SenderID.Value;

            if (!_activeGames.TryGetValue(groupId, out var game))
                return;

            if (game.CreatorId != userId)
            {
                await context.ReplyTextAsync(game.IsArabic ?
                    "فقط منشئ اللعبة يمكنه طرد اللاعبين!" :
                    "Only game creator can kick players!");
                return;
            }

            if (playerNumber < 1 || playerNumber > game.Players.Count)
            {
                await context.ReplyTextAsync(game.IsArabic ?
                    "رقم اللاعب غير صحيح!" :
                    "Invalid player number!");
                return;
            }

            var player = game.Players[playerNumber - 1];
            game.Players.RemoveAt(playerNumber - 1);

            // Adjust spy index if needed
            if (playerNumber - 1 < game.SpyIndex)
            {
                game.SpyIndex--;
            }
            else if (playerNumber - 1 == game.SpyIndex)
            {
                // Spy was kicked, choose new spy
                game.SpyIndex = _random.Next(game.Players.Count);
            }

            await context.ReplyTextAsync(game.IsArabic ?
                $"✅ تم طرد {player.Nickname}!" :
                $"✅ Kicked {player.Nickname}!");

            if (game.Players.Count < 3)
            {
                _activeGames.Remove(groupId);
                await context.Client.SendGroupMessageAsync(groupId, game.IsArabic ?
                    "/alert تم إلغاء اللعبة - عدد اللاعبين قليل جداً!" :
                    "/alert Game cancelled - too few players!");
            }
        }

        // ========== Handle Votes (Number Messages) ==========
        [Priority(int.MinValue)]
        [Command]
        public async Task HandleVote(CommandContext context, int vote)
        {
            if (context.Message.IsPrivateMessage)
                return;

            uint groupId = context.Message.RecipientID.Value;
            uint userId = context.Message.SenderID.Value;

            if (!_activeGames.TryGetValue(groupId, out var game))
                return;

            if (game.State != GameState.Playing && game.State != GameState.WaitingForContinue)
                return;

            // Handle continue/stop vote
            if (game.State == GameState.WaitingForContinue)
            {
                if (userId != game.CreatorId)
                    return;

                if (vote == 1)
                {
                    // Reset game
                    game.State = GameState.Playing;
                    game.Votes.Clear();
                    game.SecretWord = _fruits[_random.Next(_fruits.Length)];
                    game.SpyIndex = _random.Next(game.Players.Count);
                    game.LastVoteTime = null;

                    // Send player list again
                    string playerList = game.IsArabic ? "📋 قائمة اللاعبين:\n" : "📋 Players List:\n";
                    for (int i = 0; i < game.Players.Count; i++)
                    {
                        var player = game.Players[i];
                        playerList += $"{i + 1}. {player.Nickname} (ID: {player.UserId})\n";
                    }
                    await context.Client.SendGroupMessageAsync(groupId, playerList);

                    // Send new messages
                    for (int i = 0; i < game.Players.Count; i++)
                    {
                        var player = game.Players[i];
                        try
                        {
                            if (i == game.SpyIndex)
                            {
                                string spyMsg = game.IsArabic ?
                                    "/alert انت الجاسوس يا قلب قلبي اخدعهم واختار أي لاعب من القائمه عشان محد يشك فيك 🥴" :
                                    "/alert You are the spy! Deceive them and choose any player from the list so no one suspects you 🥴";
                                await context.Client.SendPrivateMessageAsync(player.UserId, spyMsg);
                            }
                            else
                            {
                                string wordMsg = game.IsArabic ?
                                    $"/alert كلمة السر هي: {game.SecretWord}" :
                                    $"/alert The secret word is: {game.SecretWord}";
                                await context.Client.SendPrivateMessageAsync(player.UserId, wordMsg);
                            }
                        }
                        catch { }
                    }

                    await context.Client.SendGroupMessageAsync(groupId, game.IsArabic ?
                        "🎮 جولة جديدة! اختاروا من تظنون أنه الجاسوس!" :
                        "🎮 New round! Choose who you think is the spy!");
                }
                else if (vote == 2)
                {
                    _activeGames.Remove(groupId);
                    await context.Client.SendGroupMessageAsync(groupId, game.IsArabic ?
                        "/alert تم إنهاء اللعبة! شكراً للعب 😊" :
                        "/alert Game ended! Thanks for playing 😊");
                }
                return;
            }

            // Handle player vote
            if (!game.Players.Any(p => p.UserId == userId))
                return;

            if (vote < 1 || vote > game.Players.Count)
                return;

            if (game.Votes.ContainsKey(userId))
                return; // Already voted

            game.Votes[userId] = vote;
            game.LastVoteTime = DateTime.UtcNow;

            // Check if all voted
            if (game.Votes.Count == game.Players.Count)
            {
                await EndGame(context, groupId);
            }
        }

        // ========== End Game and Calculate Scores ==========
        private async Task EndGame(CommandContext context, uint groupId)
        {
            if (!_activeGames.TryGetValue(groupId, out var game))
                return;

            var spy = game.Players[game.SpyIndex];

            // Reveal spy
            string revealMsg = game.IsArabic ?
                $"/alert هذا هو الخاين البواق:\n{spy.UserId} - {spy.Nickname}" :
                $"/alert This is the traitor:\n{spy.UserId} - {spy.Nickname}";

            await context.Client.SendGroupMessageAsync(groupId, revealMsg);

            // Calculate scores
            if (!_groupScores.ContainsKey(groupId))
                _groupScores[groupId] = new Dictionary<uint, int>();

            foreach (var kvp in game.Votes)
            {
                uint voterId = kvp.Key;
                int votedPlayerNumber = kvp.Value;
                
                // Initialize scores
                if (!_globalScores.ContainsKey(voterId))
                    _globalScores[voterId] = 0;
                if (!_groupScores[groupId].ContainsKey(voterId))
                    _groupScores[groupId][voterId] = 0;

                // Check if voted correctly
                if (votedPlayerNumber == game.SpyIndex + 1)
                {
                    _globalScores[voterId]++;
                    _groupScores[groupId][voterId]++;
                }
            }

            // Update spy score (lose 1 point per correct guess)
            int correctGuesses = game.Votes.Count(kvp => kvp.Value == game.SpyIndex + 1);
            if (!_globalScores.ContainsKey(spy.UserId))
                _globalScores[spy.UserId] = 0;
            if (!_groupScores[groupId].ContainsKey(spy.UserId))
                _groupScores[groupId][spy.UserId] = 0;
            
            _globalScores[spy.UserId] -= correctGuesses;
            _groupScores[groupId][spy.UserId] -= correctGuesses;

            // Ask for continue
            game.State = GameState.WaitingForContinue;
            string continueMsg = game.IsArabic ?
                "/alert اذا ودك تكمل اللعبه ارسل رقم 1 او اذا مالك خاطر ارسل رقم 2" :
                "/alert If you want to continue the game, send the number 1, or if you don't want to, send the number 2.";

            await context.Client.SendGroupMessageAsync(groupId, continueMsg);
        }

        // ========== Show Channel Ranking Commands ==========
        [Command("جاسوس ترتيب")]
        [Command("جس ترتيب")]
        public async Task ShowChannelRankingArabic(CommandContext context)
        {
            await ShowChannelRanking(context, true);
        }

        [Command("spy arrangement")]
        [Command("spy at")]
        public async Task ShowChannelRankingEnglish(CommandContext context)
        {
            await ShowChannelRanking(context, false);
        }

        private async Task ShowChannelRanking(CommandContext context, bool isArabic)
        {
            uint groupId = context.Message.RecipientID ?? context.Message.SenderID.Value;

            if (context.Message.IsPrivateMessage)
            {
                groupId = context.Message.SenderID.Value;
            }

            if (!_groupScores.ContainsKey(groupId) || !_groupScores[groupId].Any())
            {
                await context.ReplyTextAsync(isArabic ?
                    "لا توجد نقاط بعد في هذه القناة!" :
                    "No scores yet in this channel!");
                return;
            }

            var sortedScores = _groupScores[groupId]
                .OrderByDescending(kvp => kvp.Value)
                .Take(10)
                .ToList();

            string ranking = isArabic ? "🏆 ترتيب القناة:\n" : "🏆 Channel Ranking:\n";
            for (int i = 0; i < sortedScores.Count; i++)
            {
                var user = await context.Client.GetUserAsync(sortedScores[i].Key);
                ranking += $"{i + 1}. ID: {sortedScores[i].Key} - {user?.Nickname ?? "Unknown"} - {sortedScores[i].Value} {(isArabic ? "نقطة" : "points")}\n";
            }

            await context.ReplyTextAsync(ranking);
        }

        // ========== Show Global Ranking Commands ==========
        [Command("جاسوس عام")]
        [Command("جس عام")]
        public async Task ShowGlobalRankingArabic(CommandContext context)
        {
            await ShowGlobalRanking(context, true);
        }

        [Command("spy general")]
        [Command("spy gl")]
        public async Task ShowGlobalRankingEnglish(CommandContext context)
        {
            await ShowGlobalRanking(context, false);
        }

        private async Task ShowGlobalRanking(CommandContext context, bool isArabic)
        {
            uint userId = context.Message.SenderID.Value;

            if (!_globalScores.ContainsKey(userId))
            {
                await context.ReplyTextAsync(isArabic ?
                    "ليس لديك نقاط بعد!" :
                    "You don't have any points yet!");
                return;
            }

            var sortedScores = _globalScores.OrderByDescending(kvp => kvp.Value).ToList();
            int rank = sortedScores.FindIndex(kvp => kvp.Key == userId) + 1;

            await context.ReplyTextAsync(isArabic ?
                $"ترتيبك العام: {rank}" :
                $"Your global rank: {rank}");
        }

        // ========== Show Total Score Commands ==========
        [Command("جاسوس مجموع")]
        [Command("جس مجموع")]
        public async Task ShowTotalScoreArabic(CommandContext context)
        {
            await ShowTotalScore(context, true);
        }

        [Command("spy total")]
        public async Task ShowTotalScoreEnglish(CommandContext context)
        {
            await ShowTotalScore(context, false);
        }

        private async Task ShowTotalScore(CommandContext context, bool isArabic)
        {
            uint userId = context.Message.SenderID.Value;

            if (!_globalScores.ContainsKey(userId))
            {
                await context.ReplyTextAsync(isArabic ?
                    "ليس لديك نقاط بعد!" :
                    "You don't have any points yet!");
                return;
            }

            int score = _globalScores[userId];
            await context.ReplyTextAsync(isArabic ?
                $"مجموع نقاطك: {score}" :
                $"Your total score: {score}");
        }

        // ========== Help Commands ==========
        [Command("جاسوس مساعده")]
        [Command("جاسوس مساعدة")]
        [Command("جس مساعده")]
        [Command("جس مساعدة")]
        public async Task ShowHelpArabic(CommandContext context)
        {
            string help = @"📖 قائمة المساعدة:

!جاسوس جديد ، !جس جديد - لبدء لعبه جديدة
!جاسوس انظم ، !جس انظم - للانضمام للعبه
!جاسوس بدء ، !جس بدء - لبدء اللعبه
!جاسوس طرد (رقم اللاعب) ، !جس طرد (رقم اللاعب) - لطرد لاعب من اللعبه
!جاسوس ترتيب ، !جس ترتيب - لعرض ترتيب اللاعبين في القناه
!جاسوس عام ، !جس عام - لعرض ترتيب اللاعب على مستوى التطبيق
!جاسوس مجموع ، !جس مجموع - لعرض مجموع النقاط
!جاسوس مساعده ، !جس مساعده - لعرض قائمة المساعده";

            await context.ReplyTextAsync(help);
        }

        [Command("spy help")]
        public async Task ShowHelpEnglish(CommandContext context)
        {
            string help = @"📖 Help Menu:

!spy new - To start a new game
!spy join - To join the game
!spy start - To start the game
!spy kick (player number) - To kick a player from the game
!spy arrangement, !spy at - To show player ranking in the channel
!spy general, !spy gl - To show player ranking at the application level
!spy total - To show total score
!spy help - To access the help menu";

            await context.ReplyTextAsync(help);
        }
    }

    // ========== Game Data Classes ==========
    public class SpyGame
    {
        public uint GroupId { get; set; }
        public uint CreatorId { get; set; }
        public bool IsArabic { get; set; }
        public GameState State { get; set; }
        public List<Player> Players { get; set; } = new List<Player>();
        public string SecretWord { get; set; }
        public int SpyIndex { get; set; }
        public Dictionary<uint, int> Votes { get; set; } = new Dictionary<uint, int>();
        public DateTime CreatedAt { get; set; }
        public DateTime? LastVoteTime { get; set; }
    }

    public class Player
    {
        public uint UserId { get; set; }
        public string Nickname { get; set; }
    }

    public enum GameState
    {
        WaitingForPlayers,
        Playing,
        WaitingForContinue
    }
}
