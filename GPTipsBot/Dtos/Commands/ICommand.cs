using GPTipsBot.UpdateHandlers;

namespace GPTipsBot
{
    public interface ICommand
    {
        public string SlashCommand { get; }
        public List<string> CommandVariants { get; }
        public Task ApplyAsync(UpdateDecorator update);
    }
}
