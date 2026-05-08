namespace CryptoScreener.Clients
{
    public interface IExchangeClient
    {
        string ExchangeName { get; }
        void Start();
        void Stop();
    }
}