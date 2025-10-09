namespace AmescoAPI.Models
{
    public class CreateVoucherRequest
    {
        public long VoucherId { get; set; }
        public string UserId { get; set; }
        public decimal Value { get; set; }
    }
}