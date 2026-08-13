namespace Factur.Application.DTOs;

public class MonthlyReportDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int InvoiceCount { get; set; }
    public decimal TotalHT { get; set; }
    public decimal TotalTVA { get; set; }
    public decimal TotalTTC { get; set; }
    public decimal TotalCollected { get; set; }
    public decimal Outstanding { get; set; }
    public int OutstandingCount { get; set; }
    public IReadOnlyList<TVAReportDto> TVAByRate { get; set; } = new List<TVAReportDto>();
    public IReadOnlyList<InvoiceSummaryDto> Invoices { get; set; } = new List<InvoiceSummaryDto>();
}

public class TVAReportDto
{
    public string TVARate { get; set; } = string.Empty;
    public decimal TotalHT { get; set; }
    public decimal TVAAmount { get; set; }
    public decimal TotalTTC { get; set; }
}

public class TopClientDto
{
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public int InvoiceCount { get; set; }
    public decimal TotalTTC { get; set; }
}

public class YearlyPointDto
{
    public string Label { get; set; } = string.Empty;
    public int Month { get; set; }
    public decimal TotalTTC { get; set; }
    public decimal TotalTVA { get; set; }
}

public class AuditLogDto
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? ChangedData { get; set; }
    public DateTime Timestamp { get; set; }
}
