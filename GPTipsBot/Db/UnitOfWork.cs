using GPTipsBot.Repositories;

namespace GPTipsBot.Db
{
    public class UnitOfWork : IDisposable
    {
        private readonly ApplicationContext context;

        public UnitOfWork(ApplicationContext context, BotSettingsRepository
            botSettingsRepository, MessageRepository messageRepository,
            OpenaiAccountsRepository openaiAccountsRepository, UserCommandRepository userCommandRepository)
        {
            this.context = context;
            BotSettings = botSettingsRepository;
            Messages = messageRepository;
            OpenaiAccounts = openaiAccountsRepository;
            UserCommandRepository = userCommandRepository;
        }

        public BotSettingsRepository BotSettings { get; }

        public MessageRepository Messages { get; }

        public OpenaiAccountsRepository OpenaiAccounts { get; }

        public UserCommandRepository UserCommandRepository { get; }

        public void Save()
        {
            context.SaveChanges();
        }
 
        private bool disposed = false;
 
        public virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    context.Dispose();
                }
                disposed = true;
            }
        }
 
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
