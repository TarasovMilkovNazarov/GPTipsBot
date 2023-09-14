using GPTipsBot.Repositories;

namespace GPTipsBot.Db
{
    public class UnitOfWork : IDisposable
    {
        private readonly ApplicationContext context;
        private readonly BotSettingsRepository botSettingsRepository;
        private readonly MessageRepository messageRepository;

        public UnitOfWork(ApplicationContext context, BotSettingsRepository botSettingsRepository, MessageRepository messageRepository)
        {
            this.context = context;
            this.botSettingsRepository = botSettingsRepository;
            this.messageRepository = messageRepository;
        }

        public BotSettingsRepository BotSettings => botSettingsRepository;
        public MessageRepository Messages => messageRepository;
 
        public void Save()
        {
            context.SaveChanges();
        }
 
        private bool disposed = false;
 
        public virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    context.Dispose();
                }
                this.disposed = true;
            }
        }
 
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
