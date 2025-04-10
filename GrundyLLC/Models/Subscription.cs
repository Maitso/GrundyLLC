namespace GrundyLLC.Models
{
    public class Subscription
    {
        public int Id { get; set; }
        public string SubscriptionType { get; set; } // Weekly or Monthly
        public decimal Price { get; set; } // Price of the subscription
    }
}
