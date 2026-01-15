#nullable disable

using System;
using System.Threading.Tasks;
using E_Commerce.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace E_Commerce.Areas.Identity.Pages.Account.Manage
{
    public class DownloadPersonalDataModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<DownloadPersonalDataModel> _logger;

        public DownloadPersonalDataModel(
            UserManager<ApplicationUser> userManager,
            ILogger<DownloadPersonalDataModel> logger)
        {
            _userManager = userManager;
            _logger = logger;

            // Set QuestPDF license
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public IActionResult OnGet()
        {
            return NotFound();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            _logger.LogInformation("User with ID '{UserId}' asked for their personal data.", _userManager.GetUserId(User));

            // Generate PDF instead of JSON
            var pdfBytes = GeneratePersonalDataPdf(user);

            var fileName = $"PersonalData-{user.UserName}-{DateTime.Now:yyyyMMddHHmmss}.pdf";

            Response.Headers.TryAdd("Content-Disposition", $"attachment; filename={fileName}");
            return new FileContentResult(pdfBytes, "application/pdf");
        }

        private byte[] GeneratePersonalDataPdf(ApplicationUser user)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(50);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Header().Column(headerColumn =>
                    {
                        headerColumn.Spacing(10);
                        headerColumn.Item().Row(headerRow =>
                        {
                            headerRow.RelativeItem().Column(companyColumn =>
                            {
                                companyColumn.Item().Text("Fridge Frenzy")
                                    .FontSize(28).Bold().FontColor(Colors.Blue.Darken4);
                                companyColumn.Item().PaddingTop(5).Text("Personal Data Export")
                                    .FontSize(13).SemiBold().FontColor(Colors.Blue.Darken2);
                                companyColumn.Item().PaddingTop(10).Text("123 Main Street")
                                    .FontSize(9).FontColor(Colors.Grey.Darken2);
                                companyColumn.Item().Text("Port Elizabeth, Eastern Cape, 6001")
                                    .FontSize(9).FontColor(Colors.Grey.Darken2);
                                companyColumn.Item().PaddingTop(3).Text("South Africa")
                                    .FontSize(9).FontColor(Colors.Grey.Darken2);
                            });

                            headerRow.ConstantItem(180).Column(userColumn =>
                            {
                                userColumn.Item().Background(Colors.Blue.Darken4)
                                    .Padding(10).Column(invColumn =>
                                    {
                                        invColumn.Item().AlignCenter().Text("PERSONAL DATA")
                                            .FontSize(16).Bold().FontColor(Colors.White);
                                        invColumn.Item().PaddingTop(5).AlignCenter().Text($"Generated on {DateTime.Now:dd MMM yyyy}")
                                            .FontSize(10).SemiBold().FontColor(Colors.White);
                                    });
                            });
                        });

                        headerColumn.Item().PaddingTop(10).LineHorizontal(2).LineColor(Colors.Blue.Darken3);
                    });

                    page.Content().PaddingTop(20).Column(contentColumn =>
                    {
                        contentColumn.Spacing(20);

                        // Account Information Section
                        contentColumn.Item().Text("ACCOUNT INFORMATION")
                            .FontSize(12).Bold().FontColor(Colors.Blue.Darken4);

                        contentColumn.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(3);
                            });

                            table.Cell().Background(Colors.Grey.Lighten3).Padding(8)
                                .Text("Field").FontSize(10).Bold().FontColor(Colors.Blue.Darken4);
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(8)
                                .Text("Value").FontSize(10).Bold().FontColor(Colors.Blue.Darken4);

                            
                            AddTableRow(table, "Username", user.UserName);
                            AddTableRow(table, "Email", user.Email);
                            AddTableRow(table, "Email Confirmed", user.EmailConfirmed ? "Yes" : "No");
                            AddTableRow(table, "Phone Number", user.PhoneNumber ?? "Not set");
                            AddTableRow(table, "Phone Confirmed", user.PhoneNumberConfirmed ? "Yes" : "No");
                            AddTableRow(table, "Two-Factor Enabled", user.TwoFactorEnabled ? "Yes" : "No");
                            AddTableRow(table, "Lockout Enabled", user.LockoutEnabled ? "Yes" : "No");
                            AddTableRow(table, "Access Failed Count", user.AccessFailedCount.ToString());
                            AddTableRow(table, "Account Type", user.AccountType.ToString());
                        });

                        // Profile & Business Information Section
                        contentColumn.Item().PaddingTop(20).Text("PROFILE & BUSINESS INFORMATION")
                            .FontSize(12).Bold().FontColor(Colors.Blue.Darken4);

                        contentColumn.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(3);
                            });

                            table.Cell().Background(Colors.Grey.Lighten3).Padding(8)
                                .Text("Field").FontSize(10).Bold().FontColor(Colors.Blue.Darken4);
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(8)
                                .Text("Value").FontSize(10).Bold().FontColor(Colors.Blue.Darken4);

                            AddTableRow(table, "Owner Name", user.Owner ?? "Not set");
                            AddTableRow(table, "Company Name", user.CompanyName ?? "Not set");
                            AddTableRow(table, "Contact Number", user.ContactNumber ?? "Not set");
                        });

                        // Address Information Section
                        if (!string.IsNullOrEmpty(user.StreetNumber) || !string.IsNullOrEmpty(user.StreetName) ||
                            !string.IsNullOrEmpty(user.Suburb) || !string.IsNullOrEmpty(user.City))
                        {
                            contentColumn.Item().PaddingTop(20).Text("ADDRESS INFORMATION")
                                .FontSize(12).Bold().FontColor(Colors.Blue.Darken4);

                            contentColumn.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(3);
                                });

                                table.Cell().Background(Colors.Grey.Lighten3).Padding(8)
                                    .Text("Field").FontSize(10).Bold().FontColor(Colors.Blue.Darken4);
                                table.Cell().Background(Colors.Grey.Lighten3).Padding(8)
                                    .Text("Value").FontSize(10).Bold().FontColor(Colors.Blue.Darken4);

                                AddTableRow(table, "Street Number", user.StreetNumber ?? "Not set");
                                AddTableRow(table, "Street Name", user.StreetName ?? "Not set");
                                AddTableRow(table, "Suburb", user.Suburb ?? "Not set");
                                AddTableRow(table, "City", user.City ?? "Not set");
                                AddTableRow(table, "Postal Code", user.PostalCode ?? "Not set");
                            });
                        }

                        // Notes Section
                        if (!string.IsNullOrEmpty(user.Notes))
                        {
                            contentColumn.Item().PaddingTop(20).Text("ADDITIONAL NOTES")
                                .FontSize(12).Bold().FontColor(Colors.Blue.Darken4);

                            contentColumn.Item().Background(Colors.Grey.Lighten3).Padding(15)
                                .Text(user.Notes)
                                .FontSize(10).FontColor(Colors.Black);
                        }

                        // Security Information Section
                        contentColumn.Item().PaddingTop(20).Text("SECURITY & PRIVACY")
                            .FontSize(12).Bold().FontColor(Colors.Blue.Darken4);

                        contentColumn.Item().Background(Colors.Orange.Lighten5).Padding(15)
                            .Column(securityColumn =>
                            {
                                securityColumn.Item().Text("Data Privacy Notice")
                                    .FontSize(10).Bold().FontColor(Colors.Orange.Darken3);
                                securityColumn.Item().PaddingTop(5).Text("This document contains your personal information. Please keep it secure and do not share it with unauthorized parties.")
                                    .FontSize(9).FontColor(Colors.Grey.Darken2);
                                securityColumn.Item().PaddingTop(3).Text("If you believe your data has been compromised, please contact us immediately.")
                                    .FontSize(9).FontColor(Colors.Grey.Darken2);
                                securityColumn.Item().PaddingTop(5).Text("According to our records, your account type is: " + user.AccountType.ToString())
                                    .FontSize(9).SemiBold().FontColor(Colors.Blue.Darken3);
                            });

                        // System Information Section
                        contentColumn.Item().PaddingTop(20).Text("SYSTEM INFORMATION")
                            .FontSize(12).Bold().FontColor(Colors.Blue.Darken4);

                        contentColumn.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(3);
                            });

                            table.Cell().Background(Colors.Grey.Lighten3).Padding(8)
                                .Text("Field").FontSize(10).Bold().FontColor(Colors.Blue.Darken4);
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(8)
                                .Text("Value").FontSize(10).Bold().FontColor(Colors.Blue.Darken4);

                            AddTableRow(table, "Data Generated On", DateTime.Now.ToString("dd MMMM yyyy HH:mm:ss"));
                            AddTableRow(table, "Data Format", "PDF Document");
                            AddTableRow(table, "Purpose", "Personal Data Export - GDPR/Privacy Request");
                            AddTableRow(table, "User Role", user.AccountType == AccountType.Business ? "Business Customer" : "Individual Customer");
                        });
                    });

                    page.Footer().Column(footerColumn =>
                    {
                        footerColumn.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                        footerColumn.Item().PaddingTop(15).AlignCenter()
                            .Text("Fridge Frenzy - Your Privacy Matters")
                            .FontSize(11).SemiBold().FontColor(Colors.Blue.Darken3);
                        footerColumn.Item().PaddingTop(5).AlignCenter()
                            .Text("For privacy concerns, contact: privacy@fridgefrenzy.com")
                            .FontSize(8).FontColor(Colors.Grey.Darken1);
                        footerColumn.Item().PaddingTop(8).AlignCenter()
                            .Text("© 2025 Fridge Frenzy - All Rights Reserved | This is an official data export")
                            .FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
                        footerColumn.Item().PaddingTop(10).AlignCenter().Text(text =>
                        {
                            text.Span("Page ").FontSize(8).FontColor(Colors.Grey.Medium);
                            text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken2);
                            text.Span(" of ").FontSize(8).FontColor(Colors.Grey.Medium);
                            text.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken2);
                        });
                    });
                });
            });

            return document.GeneratePdf();
        }

        private void AddTableRow(TableDescriptor table, string field, string value)
        {
            bool alternate = false;
            var bgColor = alternate ? Colors.Grey.Lighten4 : Colors.White;
            alternate = !alternate;

            table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                .Padding(8).Text(field).FontSize(9).FontColor(Colors.Grey.Darken2);
            table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                .Padding(8).Text(value ?? "Not set").FontSize(9).FontColor(Colors.Black);
        }
    }
}