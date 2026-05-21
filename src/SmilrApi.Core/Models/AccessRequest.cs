namespace SmilrApi.Core.Models;

public class AccessRequest
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Company { get; set; }
    public string UseCase { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public int Status { get; set; } = 0;   // 0=Requested, 1=Granted, 2=Rejected
    public int? ApiKeyId { get; set; }
    public ApiKey? ApiKey { get; set; }
}
