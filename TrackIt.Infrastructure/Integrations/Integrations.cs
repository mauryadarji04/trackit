using System.Net.Http.Json;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TrackIt.Core.Dtos;
using TrackIt.Core.Interfaces;

namespace TrackIt.Infrastructure.Integrations;

public class SmtpOptions
{
    public string Host { get; set; } = string.Empty;   // empty = email disabled (messages are only logged)
    public int Port { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "trackit@example.com";
    public string FromName { get; set; } = "TrackIt";
    public bool UseStartTls { get; set; } = true;
}

/// <summary>Sends mail with MailKit. With no SMTP host configured it only logs, so the app runs without a mail server.</summary>
public class SmtpEmailService : IEmailService
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<SmtpOptions> options, ILogger<SmtpEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string toAddress, string subject, string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            _logger.LogInformation("Email disabled (Smtp:Host empty). Would send to {To}: {Subject}", toAddress, subject);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(toAddress));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(_options.Host, _options.Port,
            _options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto);
        if (!string.IsNullOrEmpty(_options.Username))
            await client.AuthenticateAsync(_options.Username, _options.Password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}

public class WebhookOptions
{
    public string Url { get; set; } = string.Empty;       // empty = webhook disabled
    public string TicketBaseUrl { get; set; } = string.Empty; // e.g. https://localhost:5001/Tickets/Details/
}

/// <summary>Posts to a Slack or Teams incoming webhook. Both accept {"text": "..."}.</summary>
public class WebhookService : IWebhookService
{
    private readonly HttpClient _http;
    private readonly WebhookOptions _options;
    private readonly ILogger<WebhookService> _logger;

    public WebhookService(HttpClient http, IOptions<WebhookOptions> options, ILogger<WebhookService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task NotifyCriticalTicketAsync(int ticketId, string title, string projectName)
    {
        if (string.IsNullOrWhiteSpace(_options.Url))
        {
            _logger.LogInformation("Webhook disabled (Webhook:Url empty). Critical ticket #{Id}: {Title}", ticketId, title);
            return;
        }

        var link = string.IsNullOrWhiteSpace(_options.TicketBaseUrl) ? "" : $" {_options.TicketBaseUrl}{ticketId}";
        var payload = new { text = $":rotating_light: Critical ticket #{ticketId} in {projectName}: {title}{link}" };

        var response = await _http.PostAsJsonAsync(_options.Url, payload);
        response.EnsureSuccessStatusCode();
    }
}

/// <summary>Builds the weekly defect summary PDF in code with QuestPDF (Community licence).</summary>
public class QuestPdfReportGenerator : IPdfReportGenerator
{
    static QuestPdfReportGenerator() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] BuildWeeklyReport(WeeklySummary s)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(t => t.FontSize(11));

                page.Header().Column(col =>
                {
                    col.Item().Text("TrackIt - Weekly Defect Summary").FontSize(20).Bold();
                    col.Item().Text($"{s.WeekStart:dd MMM yyyy} to {s.WeekEnd.AddDays(-1):dd MMM yyyy}").FontColor(Colors.Grey.Darken2);
                });

                page.Content().PaddingVertical(16).Column(col =>
                {
                    col.Spacing(14);

                    col.Item().Row(row =>
                    {
                        Tile(row, "Opened", s.Opened);
                        Tile(row, "Resolved", s.Resolved);
                        Tile(row, "Closed", s.Closed);
                        Tile(row, "Still open", s.StillOpen);
                        Tile(row, "SLA breaches", s.SlaBreaches);
                    });

                    Section(col, "New tickets by priority", s.ByPriority);
                    Section(col, "New tickets by project", s.ByProject);
                    Section(col, "Resolved by developer", s.ResolvedByDeveloper);
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span($"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC - page ");
                    t.CurrentPageNumber();
                });
            });
        }).GeneratePdf();
    }

    private static void Tile(RowDescriptor row, string label, int value)
    {
        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(8).Column(c =>
        {
            c.Item().Text(value.ToString()).FontSize(22).Bold();
            c.Item().Text(label).FontSize(9).FontColor(Colors.Grey.Darken2);
        });
    }

    private static void Section(ColumnDescriptor col, string title, List<LabelCount> rows)
    {
        col.Item().Column(c =>
        {
            c.Item().Text(title).FontSize(13).SemiBold();
            if (rows.Count == 0)
            {
                c.Item().Text("No data for this week.").FontColor(Colors.Grey.Darken1);
                return;
            }

            c.Item().Table(table =>
            {
                table.ColumnsDefinition(d => { d.RelativeColumn(4); d.RelativeColumn(1); });
                table.Header(h =>
                {
                    h.Cell().BorderBottom(1).PaddingVertical(3).Text("Name").Bold();
                    h.Cell().BorderBottom(1).PaddingVertical(3).AlignRight().Text("Count").Bold();
                });
                foreach (var r in rows)
                {
                    table.Cell().PaddingVertical(2).Text(r.Label);
                    table.Cell().PaddingVertical(2).AlignRight().Text(r.Total.ToString());
                }
            });
        });
    }
}
