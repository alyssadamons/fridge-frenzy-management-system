namespace E_Commerce.Client
{
    public class PayPalClient
    {
        public string Mode { get; }
        public string ClientId { get; }
        public string ClientSecret { get; }
        public string BaseUrl => Mode == "Live" ? "https://api.paypal.com" : "https://api.sandbox.paypal.com";
        public PayPalClient(string mode, string clientId, string clientSecret)
        {
            Mode = mode ?? "Sandbox";
            ClientId = clientId;
            ClientSecret = clientSecret;
        }
    }
}
