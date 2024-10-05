using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using System.Text.Json;

namespace BankingApi.EventReceiver
{
    /// <summary>
    /// Represents a worker that listens to messages from service bus and processes them.
    /// </summary>
    public class MessageWorker
    {
        private readonly IServiceBusReceiver _serviceBusReceiver;
        private readonly BankingApiDbContext _dbContext;
        private readonly Processor _processor;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageWorker"/> class.
        /// </summary>
        /// <param name="serviceBusReceiver">Service bus to fetch messages.</param>
        /// <param name="dbContext">The database context for accessing the data.</param>
        /// <param name="processor">The processor responsible for handling credit and debit messages.</param>
        /// <param name="logger">The logger for information, warnings, and errors.</param>
        public MessageWorker(IServiceBusReceiver serviceBusReceiver, BankingApiDbContext dbContext, Processor processor, ILogger logger)
        {
            _serviceBusReceiver = serviceBusReceiver;
            _dbContext = dbContext;
            _processor = processor;
            _logger = logger;
        }

        /// <summary>
        /// Starts the message processing loop, listening for messages indefinitely.
        /// </summary>
        /// <returns></returns>
        public async Task Start()
        {
            // Implement logic to listen to messages here

            while (true) 
            {
                _logger.LogInformation("Peeking for new messages...");

                var message = await _serviceBusReceiver.Peek();

                if (message == null)
                {
                    // If IEventReceiver.Peek returns null, it means there are no messages in the queue, so await 10 seconds
                    await Task.Delay(10000);
                    continue;
                }

                try
                {
                    if (string.IsNullOrWhiteSpace(message.MessageBody))
                    {
                        await _serviceBusReceiver.MoveToDeadLetter(message);
                        continue;
                    }

                    var deserializedMessage = JsonSerializer.Deserialize<DeserializedMessage>(message.MessageBody);

                    if (deserializedMessage == null || string.IsNullOrWhiteSpace(deserializedMessage.MessageType))
                    {
                        await _serviceBusReceiver.MoveToDeadLetter(message);
                        continue;
                    }

                    using (var transaction = await _dbContext.Database.BeginTransactionAsync())
                    {
                        try
                        {
                            // Credit messages: Add amount to the existing balance
                            if (deserializedMessage.MessageType == "Credit")
                            {
                                await _processor.ProcessCredit(deserializedMessage);
                            }
                            // Debit messages: Deduct amount from the existing balance
                            else if (deserializedMessage.MessageType == "Debit")
                            {
                                await _processor.ProcessDebit(deserializedMessage);
                            }
                            else
                            {
                                _logger.LogWarning($"Received a message with invalid MessageType: {deserializedMessage.MessageType} for MessageId: {deserializedMessage.Id}. Moving message to DeadLetter.");
                                await _serviceBusReceiver.MoveToDeadLetter(message);
                            }

                            await _serviceBusReceiver.Complete(message);
                            await transaction.CommitAsync();
                        }
                        catch (Exception ex)
                        {
                            await transaction.RollbackAsync();
                            throw;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while processing messages.");

                    // Transient failures needs to be exponentially retried by 5, 25 and 125 seconds.
                    if (IsTransientError(ex))
                    {
                        for (int retryCount = 0; retryCount < 3; retryCount++)
                        {
                            int delay = retryCount switch
                            {
                                0 => 5000,
                                1 => 25000,
                                2 => 125000,
                                _ => 0
                            };

                            _logger.LogInformation($"Transient error occurred: {ex.Message}. Attempting retry {retryCount + 1} in {delay}ms.");

                            await Task.Delay(delay);
                        }
                    }
                    else
                    {
                        _logger.LogError($"Non-transient error occurred: {ex.Message}. Moving message to DeadLetter.");

                        await _serviceBusReceiver.MoveToDeadLetter(message);
                    }
                }
            }
        }

        /// <summary>
        /// Determines whether the specific exception is transient.
        /// </summary>
        /// <param name="ex"></param>
        /// <returns><c>true</c> if the exception is transient; otherwise, <c>false</c>.</returns>
        private bool IsTransientError(Exception ex)
        {
            return ex is TimeoutException || ex is SocketException; 
        }
    }
}
