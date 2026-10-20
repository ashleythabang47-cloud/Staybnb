namespace Staybnb.Services
{
    public class PriceBreakdown
    {
        public int Nights { get; set; }
        public decimal Subtotal { get; set; }     // PricePerNight x Nights
        public decimal CleaningFee { get; set; }
        public decimal ServiceFee { get; set; }
        public decimal Total { get; set; }        // Subtotal + CleaningFee + ServiceFee
    }

    public static class PricingCalculator
    {
        // Platform service fee charged on the nightly subtotal
        public const decimal ServiceFeeRate = 0.12m;

        // TotalPrice = (PricePerNight x Nights) + CleaningFee + ServiceFee
        public static PriceBreakdown Calculate(decimal pricePerNight, decimal cleaningFee, int nights)
        {
            var subtotal = pricePerNight * nights;
            var serviceFee = Math.Round(subtotal * ServiceFeeRate, 2);

            return new PriceBreakdown
            {
                Nights = nights,
                Subtotal = subtotal,
                CleaningFee = cleaningFee,
                ServiceFee = serviceFee,
                Total = subtotal + cleaningFee + serviceFee
            };
        }
    }
}
