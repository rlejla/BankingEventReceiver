namespace BankingApi.EventReceiver
{
    public class DeserializedMessage
    {
        public Guid Id { get; set; }
        public string MessageType { get; set; }
        public Guid BankAccountId { get; set; }
        public decimal Amount { get; set; }
    }
}
