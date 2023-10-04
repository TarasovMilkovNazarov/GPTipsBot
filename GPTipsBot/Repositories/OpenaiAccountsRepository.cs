using GPTipsBot.Db;
using GPTipsBot.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GPTipsBot.Repositories
{
    public class OpenaiAccountsRepository
    {
        private readonly ILogger<OpenaiAccountsRepository> logger;
        private readonly ApplicationContext context;

        public OpenaiAccountsRepository(ILogger<OpenaiAccountsRepository> logger, ApplicationContext context)
        {
            this.logger = logger;
            this.context = context;
        }
        
        public void FreezeToken(string token)
        {
            context.OpenaiAccounts.Where(x => x.Token == token)
                .ExecuteUpdate(x => x.SetProperty(y => y.FreezedAt, DateTime.UtcNow));
        }

        public void Remove(string token, DeletionReason reason)
        {
            context.OpenaiAccounts.Where(x => x.Token == token)
                .ExecuteUpdate(x => x.SetProperty(y => y.IsDeleted, true));

            context.OpenaiAccounts.Where(x => x.Token == token)
                .ExecuteUpdate(x => x.SetProperty(y => y.DeletionReason, reason));
        }

        public List<string> UnfreezeTokens()
        {
            var freezedTokens = context.OpenaiAccounts.Where(x => !x.IsDeleted && x.FreezedAt != null);
            freezedTokens.ExecuteUpdate(x => x.SetProperty(y => y.FreezedAt, default(DateTime?)));

            return freezedTokens.Select(x => x.Token).ToList();
        }
        
        public IEnumerable<OpenaiAccount> GetAll()
        {
            return context.OpenaiAccounts.AsNoTracking().ToList();
        }

        public IEnumerable<OpenaiAccount> GetAllAvailable()
        {
            return context.OpenaiAccounts.AsNoTracking().Where(x => !x.IsDeleted && x.FreezedAt == null).ToList();
        }
    }
}
