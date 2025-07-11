using GPTipsBot.Repositories;

namespace GPTipsBot.Db
{
    public class UnitOfWork : IDisposable
    {
        private readonly ApplicationContext _context;

        public UnitOfWork(ApplicationContext context, BotSettingsRepository
            botSettingsRepository, MessageRepository messageRepository,
            OpenaiAccountsRepository openaiAccountsRepository, UserCommandRepository userCommandRepository)
        {
            _context = context;
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
            _context.SaveChanges();
        }
 
        private bool _disposed = false;
 
        public virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _context.Dispose();
                }
                _disposed = true;
            }
        }
 
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
