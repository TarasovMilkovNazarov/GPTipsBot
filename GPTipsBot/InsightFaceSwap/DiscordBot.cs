using Discord.WebSocket;
using Discord;
using GPTipsBot.InsightFaceSwap.FaceSwap;

namespace GPTipsBot.InsightFaceSwap
{
    public class DiscordBot
    {
        private DiscordSocketClient _client;

        public async Task MainAsync()
        {
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
            };
            _client = new DiscordSocketClient(config);

            _client.Log += LogAsync;

            await _client.LoginAsync(TokenType.Bot, DiscordSettings.BotToken);
            await _client.StartAsync();

            _client.MessageReceived += HandleMessageAsync;
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log);
            return Task.CompletedTask;
        }

        private async Task HandleMessageAsync(SocketMessage message)
        {
            if (message is not IUserMessage userMessage)
                return;

            foreach (var attachment in userMessage.Attachments)
            {
                Console.WriteLine($"Attachment URL: {attachment.Url}");
            }
        }

        public void SendMessage()
        {
            _client.GetGuild(DiscordSettings.Guild).GetTextChannel(DiscordSettings.ChannelId).SendMessageAsync(
    "Message");
        }

        public async Task SendFile()
        {
            var guild = _client.Guilds.Single(g => g.Name == "guild name");
            var channel = guild.TextChannels.Single(ch => ch.Name == "channel name");
            await channel.SendFileAsync("C:\\Pictures\\something.png","Caption goes here");
        }
    }
}
