using BankingApi.EventReceiver;
using Microsoft.Extensions.Logging;
using Moq;

namespace BankingApi.Tests
{
    /*
    Going off of the job posting where TDD is encouraged,
    I have introduced tests for the methods in the Processing class
    */

    /// <summary>
    /// Tests for the <see cref="Processor"/> class, specifically for credit and debit processing methods.
    /// </summary>
    public class ProcessingTests
    {
        private readonly Mock<BankingApiDbContext> _mockDbContext;
        private readonly Mock<ILogger<Processor>> _mockLogger;
        private readonly Processor _processor;

        public ProcessingTests()
        {
            _mockDbContext = new Mock<BankingApiDbContext>();
            _mockLogger = new Mock<ILogger<Processor>>();
            _processor = new Processor(_mockDbContext.Object, _mockLogger.Object);
        }

        /// <summary>
        /// Tests the <see cref="Processor.ProcessCredit"/> method to ensure it correctly increases the balance of the bank account when processing a credit transaction.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task ProcessCredit_IncreasedBalance()
        {
            var bankAccount = new BankAccount { Id = Guid.NewGuid(), Balance = 300m };
            
            var initialBalance = bankAccount.Balance;
            
            var deserializedMessage = new DeserializedMessage
            {
                Id = Guid.NewGuid(),
                MessageType= "Credit",
                BankAccountId = bankAccount.Id,
                Amount = 90m
            };

            await _processor.ProcessCredit(deserializedMessage);

            Assert.Equal(initialBalance + deserializedMessage.Amount, bankAccount.Balance);
            
            _mockDbContext.Verify(db => db.SaveChangesAsync(default), Times.Once);

            _mockLogger.Verify(log => log.LogInformation(
                It.IsAny<string>(),
                deserializedMessage.Amount, bankAccount.Id, bankAccount.Balance),
                Times.Once);
        }

        /// <summary>
        /// Tests the <see cref="Processor.ProcessDebit"/> method to ensure it correctly decreases the balance of the bank account when processing a credit transaction.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task ProcessDebit_DecreasedBalance()
        {
            var bankAccount = new BankAccount { Id = Guid.NewGuid(), Balance = 300m };

            var initialBalance = bankAccount.Balance;

            var deserializedMessage = new DeserializedMessage
            {
                Id = Guid.NewGuid(),
                MessageType = "Debit",
                BankAccountId = bankAccount.Id,
                Amount = 90m
            };

            await _processor.ProcessDebit(deserializedMessage);

            Assert.Equal(initialBalance - deserializedMessage.Amount, bankAccount.Balance);

            _mockDbContext.Verify(db => db.SaveChangesAsync(default), Times.Once);

            _mockLogger.Verify(log => log.LogInformation(
                It.IsAny<string>(),
                deserializedMessage.Amount, bankAccount.Id, bankAccount.Balance),
                Times.Once);
        }
    }
}
