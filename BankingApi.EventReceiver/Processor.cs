using Microsoft.Extensions.Logging;

namespace BankingApi.EventReceiver
{
    /// <summary>
    /// Processes banking messages for credit and debit operations.
    /// </summary>
    public class Processor
    {
        private readonly BankingApiDbContext _dbContext;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="Processor"/> class.
        /// </summary>
        /// <param name="dbContext">The database context for accessing the data.</param>
        /// <param name="logger">The logger for information, warnings, and errors.</param>
        public Processor(BankingApiDbContext dbContext, ILogger logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Processes a credit operation for a specific message.
        /// </summary>
        /// <param name="deserializedMessage"></param>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException"></exception>
        public async Task ProcessCredit(DeserializedMessage deserializedMessage)
        {
            var bankAccount = await _dbContext.BankAccounts.FindAsync(deserializedMessage.BankAccountId);
            
            if (bankAccount != null)
            {
                // Credit messages: Add amount to the existing balance
                bankAccount.Balance += deserializedMessage.Amount;
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Processed credit of {deserializedMessage.Amount} for account ID {deserializedMessage.BankAccountId}. New balance: {bankAccount.Balance}.");
            }
            else
            {
                _logger.LogError($"Credit processing failed: BankAccountId {deserializedMessage.BankAccountId} not found.");
                
                throw new KeyNotFoundException($"Bank account with BankAccountId {deserializedMessage.BankAccountId} not found for credit processing.");

            }
        }

        /// <summary>
        /// Processes a debit operation for a specific message.
        /// </summary>
        /// <param name="deserializedMessage"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="KeyNotFoundException"></exception>
        public async Task ProcessDebit(DeserializedMessage deserializedMessage)
        {
            var bankAccount = await _dbContext.BankAccounts.FindAsync(deserializedMessage.BankAccountId);

            if (bankAccount != null)
            {
                //Debit messages: Deduct amount from the existing balance
                var newBalance = bankAccount.Balance - deserializedMessage.Amount;
                
                if (newBalance < 0)
                {
                    _logger.LogWarning($"Attempted debit of {deserializedMessage.Amount} for the bank account Id {deserializedMessage.BankAccountId} would result in a negative balance. Current balance: {bankAccount.Balance}.");

                    throw new InvalidOperationException($"Insufficient funds for the bank account Id {deserializedMessage.BankAccountId}. Current balance: {bankAccount.Balance}, attempted debit: {deserializedMessage.Amount}.");
                }

                bankAccount.Balance -= deserializedMessage.Amount;
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Processed debit of {deserializedMessage.Amount} for bank account Id {deserializedMessage.BankAccountId}. New balance: {bankAccount.Balance}.");
            }
            else
            {
                _logger.LogError($"Debit processing failed: bank account Id {deserializedMessage.BankAccountId} not found.");

                throw new KeyNotFoundException($"Bank account with bank account Id {deserializedMessage.BankAccountId} not found for credit processing.");

            }
        }
    }
}
