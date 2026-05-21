namespace SmilrApi.Core.Models;

public class BusinessLocation
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public int Navnelbnr { get; set; }
    public DateTime AddedAt { get; set; }

    public Business Business { get; set; } = null!;
}
