using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NuGet.Packaging;
using Recruit_Finder_AI.Data;
using Recruit_Finder_AI.Entities;
using Recruit_Finder_AI.Models;
using Recruit_Finder_AI.Services;
using System.Drawing;
using System.Security.Claims;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;
using Recruit_Finder_AI.DTO;

namespace Recruit_Finder_AI.Controllers
{
    [Authorize]
    public class CvController : Controller
    {
        private readonly Recruit_Finder_AIContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ConnectionFactory _rabbitFactory;
        private readonly NotificationService _notificationService;

        public CvController(Recruit_Finder_AIContext context,
                            UserManager<ApplicationUser> userManager,
                            IHttpClientFactory httpClientFactory,
                            IConfiguration configuration,
                            ConnectionFactory rabbitFactory,
                            NotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _rabbitFactory = rabbitFactory;
            _notificationService = notificationService;
        }
        [HttpGet]
        public async Task<IActionResult> GetStatus(int id)
        {
            var userId = _userManager.GetUserId(User);
            var cv = await _context.Cvs
                .Where(c => c.Id == id && c.UserId == userId)
                .Select(c => new { c.IsVerified, c.AiFeedback })
                .FirstOrDefaultAsync();

            if (cv == null) return NotFound();
            return Json(cv);
        }



        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        [HttpPost("/Cv/UpdateAiResult")]
        public async Task<IActionResult> UpdateAiResult([FromBody] CvVerificationDto dto)
        {
            if (dto == null) return BadRequest("Payload is null");

            var cvFromDb = await _context.Cvs
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == dto.Id);

            if (cvFromDb == null)
            {
                return NotFound($"CV with ID {dto.Id} not found.");
            }

            cvFromDb.IsVerified = dto.IsVerified;
            cvFromDb.AiFeedback = dto.AiFeedback;
            _context.Cvs.Update(cvFromDb);
            await _context.SaveChangesAsync();

            try
            {
                var notification = new Notification
                {
                    UserId = cvFromDb.UserId,
                    Title = "AI Analysis Completed",
                    Content = $"The AI verification for your CV '{cvFromDb.Name} {cvFromDb.Surname}' is finished.",
                    ActionUrl = $"/Cv/Details/{cvFromDb.Id}",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false,
                    IsCompleted = false
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NOTIFICATION ERROR]: {ex.Message}");
            }

            return Ok(new { message = "CV updated successfully and notification created" });
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var myCvs = await _context.Cvs
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return View(myCvs);
        }

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Surname,DateOfBirth,Address,PhoneNumber,Email,ProfessionalExperience,Education,Portfolio,Languages,Skills,Interests")] Cv cv)
        {
            var userId = _userManager.GetUserId(User);
            ModelState.Remove("User");
            ModelState.Remove("UserId");
            ModelState.Remove("Title");

            if (ModelState.IsValid)
            {
                var cvCount = await _context.Cvs.CountAsync(c => c.UserId == userId);
                if (cvCount >= 5)
                {
                    TempData["ErrorMessage"] = "Limit of 5 CVs reached.";
                    return RedirectToAction(nameof(Index));
                }

                cv.UserId = userId;
                cv.CreatedAt = DateTime.UtcNow;
                cv.IsVerified = false;

                _context.Add(cv);
                await _context.SaveChangesAsync();

                _ = SendToAiVerification(cv);

                TempData["SuccessMessage"] = "CV created! AI analysis started.";
                return RedirectToAction(nameof(Index));
            }
            return View(cv);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify(int id)
        {
            var userId = _userManager.GetUserId(User);
            var cv = await _context.Cvs.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

            if (cv == null)
            {
                TempData["ErrorMessage"] = "CV not found.";
                return RedirectToAction(nameof(Index));
            }
            cv.IsVerified = false;
            cv.AiFeedback = null;
            _context.Cvs.Update(cv);
            await _context.SaveChangesAsync();

            await SendToAiVerification(cv);

            TempData["SuccessMessage"] = "AI analysis triggered in the background!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User);
            var cv = await _context.Cvs.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);

            if (cv != null)
            {
                _context.Cvs.Remove(cv);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "CV deleted.";
            }
            return RedirectToAction(nameof(Index));
        }
        public async Task<IActionResult> Details(int id)
        {
            var userId = _userManager.GetUserId(User);
            var cv = await _context.Cvs.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);
            if (cv == null) return NotFound();
            return View(cv);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var userId = _userManager.GetUserId(User);
            var cv = await _context.Cvs.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);
            if (cv == null) return NotFound();
            return View(cv);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Surname,DateOfBirth,Address,PhoneNumber,Email,ProfessionalExperience,Education,Portfolio,Languages,Skills,Interests")] Cv cv)
        {
            var userId = _userManager.GetUserId(User);
            var existingCv = await _context.Cvs.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

            if (existingCv == null) return NotFound();

            ModelState.Remove("User");
            ModelState.Remove("UserId");
            ModelState.Remove("Title");

            if (ModelState.IsValid)
            {
                cv.UserId = userId;
                cv.CreatedAt = existingCv.CreatedAt;

                cv.IsVerified = false;
                cv.AiFeedback = null;

                _context.Update(cv);
                await _context.SaveChangesAsync();

<<<<<<< HEAD
                _ = SendToAiVerification(cv);

                TempData["SuccessMessage"] = "CV updated and re-queued for analysis!";
=======
                TempData["SuccessMessage"] = "CV updated and verification status has been reset!";
>>>>>>> f9c0fb8 (Adding architecture to Docker, PostgreSQL, RabbitMQ and UI)
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                foreach (var modelState in ModelState.Values)
                {
                    foreach (var error in modelState.Errors)
                    {
                        System.Diagnostics.Debug.WriteLine("BŁĄD WALIDACJI: " + error.ErrorMessage);
                    }
                }
            }

            return View(cv);
        }
        private async Task<bool> SendToAiVerification(Cv cv)
        {
            System.Diagnostics.Debug.WriteLine($"DEBUG: Attempting to connect to RabbitMQ host: {_rabbitFactory.HostName}");

            try
            {
                using var connection = await _rabbitFactory.CreateConnectionAsync();
                using var channel = await connection.CreateChannelAsync();

                await channel.QueueDeclareAsync(
                    queue: "cv_verification_queue",
                    durable: true,
                    exclusive: false,
                    autoDelete: false
                );

                var message = new
                {
                    cvId = cv.Id,
                    fullName = $"{cv.Name} {cv.Surname}",
                    dateOfBirth = cv.DateOfBirth?.ToString("yyyy-MM-dd"),
                    experience = cv.ProfessionalExperience,
                    education = cv.Education,
                    skills = cv.Skills,
                    languages = cv.Languages,
                    portfolio = cv.Portfolio
                };

                var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

                await channel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: "cv_verification_queue",
                    body: body
                );
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error connecting to RabbitMQ: {ex.Message}");
                return false;
            }
        }

        [HttpGet]
        public async Task<IActionResult> DownloadPdf(int id)
        {
            var cv = await _context.Cvs.FirstOrDefaultAsync(m => m.Id == id && m.UserId == _userManager.GetUserId(User));
            if (cv == null) return NotFound();

            var pdf = GenerateCvDocument(cv);
            byte[] pdfBytes = pdf.GeneratePdf();
            return File(pdfBytes, "application/pdf", $"CV_{cv.Name}_{cv.Surname}.pdf");
        }

        [HttpGet]
        public async Task<IActionResult> PreviewPdf(int id)
        {
            var cv = await _context.Cvs.FirstOrDefaultAsync(m => m.Id == id && m.UserId == _userManager.GetUserId(User));
            if (cv == null) return NotFound();

            var pdf = GenerateCvDocument(cv);
            byte[] pdfBytes = pdf.GeneratePdf();
            return File(pdfBytes, "application/pdf");
        }

        private IDocument GenerateCvDocument(Cv cv)
        {
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            var primaryColor = Colors.Blue.Darken4;
            var accentColor = Colors.Grey.Lighten3;
            var textColor = Colors.BlueGrey.Darken4;
            var mutedText = Colors.BlueGrey.Lighten1;

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Verdana).FontColor(textColor));

                    page.Content().Row(row =>
                    {
                        row.ConstantItem(180).Background(Colors.Grey.Lighten5).Padding(20).Column(col =>
                        {
                            col.Item().AlignCenter().Width(100).Height(100).Background(accentColor);
                            col.Item().PaddingVertical(20);

                            col.Item().Text("CONTACT").FontSize(11).ExtraBold().FontColor(primaryColor).LetterSpacing(0.1f);
                            col.Item().PaddingVertical(5).LineHorizontal(1.5f).LineColor(primaryColor);

                            var contactInfo = new[] {
                                ("Phone", cv.PhoneNumber),
                                ("E-mail", cv.Email),
                                ("Address", cv.Address)
                            };

                            foreach (var (label, value) in contactInfo)
                            {
                                if (!string.IsNullOrEmpty(value))
                                {
                                    col.Item().PaddingTop(8).Column(c => {
                                        c.Item().Text(label).FontSize(7).SemiBold().FontColor(mutedText);
                                        c.Item().Text(value).FontSize(9);
                                    });
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(cv.Languages))
                            {
                                col.Item().PaddingTop(25).Text("LANGUAGES").FontSize(11).ExtraBold().FontColor(primaryColor);
                                col.Item().PaddingVertical(5).LineHorizontal(1.5f).LineColor(primaryColor);
                                foreach (var lang in cv.Languages.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    col.Item().Text(lang.Trim()).FontSize(9);
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(cv.Skills))
                            {
                                col.Item().PaddingTop(25).Text("SKILLS").FontSize(11).ExtraBold().FontColor(primaryColor);
                                col.Item().PaddingVertical(5).LineHorizontal(1.5f).LineColor(primaryColor);
                                foreach (var skill in cv.Skills.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    col.Item().Text(skill.Trim()).FontSize(9);
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(cv.Interests))
                            {
                                col.Item().PaddingTop(25).Text("INTERESTS").FontSize(11).ExtraBold().FontColor(primaryColor);
                                col.Item().PaddingVertical(5).LineHorizontal(1.5f).LineColor(primaryColor);
                                foreach (var hobby in cv.Interests.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    col.Item().Text(hobby.Trim()).FontSize(9);
                                }
                            }
                        });

                        row.RelativeItem().Padding(35).Column(col =>
                        {
                            col.Item().Row(r => {
                                r.RelativeItem().Column(c => {
                                    c.Item().Text($"{cv.Name} {cv.Surname}").FontSize(26).ExtraBold().FontColor(primaryColor);
                                    c.Item().PaddingTop(2).Text("CANDIDATE").FontSize(12).Medium().FontColor(mutedText).LetterSpacing(0.2f);
                                });
                            });

                            col.Item().PaddingVertical(15);

                            void BuildSectionHeader(string title)
                            {
                                col.Item().PaddingTop(15).Column(c => {
                                    c.Item().Text(title.ToUpper()).FontSize(13).ExtraBold().FontColor(primaryColor).LetterSpacing(0.1f);
                                    c.Item().PaddingVertical(4).LineHorizontal(1.5f).LineColor(primaryColor);
                                });
                            }

                            if (!string.IsNullOrWhiteSpace(cv.ProfessionalExperience))
                            {
                                BuildSectionHeader("Experience");

                                var expEntries = cv.ProfessionalExperience.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
                                foreach (var entry in expEntries)
                                {
                                    col.Item().PaddingTop(10).Row(rowExp =>
                                    {
                                        rowExp.ConstantItem(15).Layers(layers =>
                                        {
                                            layers.PrimaryLayer().AlignCenter().LineVertical(0.5f).LineColor(accentColor);
                                            layers.Layer().AlignCenter().PaddingTop(4).Component(new TimePoint(primaryColor));
                                        });

                                        rowExp.RelativeItem().PaddingLeft(10).PaddingBottom(10).Column(textCol =>
                                        {
                                            var parts = entry.Split(new[] { ':', ']' }, StringSplitOptions.RemoveEmptyEntries);
                                            if (parts.Length >= 2)
                                            {
                                                var date = parts[0].Replace("[", "").Trim();
                                                var name = parts[1].Trim();
                                                var desc = parts.Length > 2 ? string.Join(":", parts.Skip(2)).Trim() : "";

                                                textCol.Item().Text(name).FontSize(11).Bold();
                                                textCol.Item().Text(date).FontSize(8).SemiBold().FontColor(primaryColor);
                                                if (!string.IsNullOrEmpty(desc))
                                                    textCol.Item().PaddingTop(3).Text(desc).FontSize(9).FontColor(Colors.Grey.Darken2).LineHeight(1.3f);
                                            }
                                            else textCol.Item().Text(entry.Trim()).FontSize(9);
                                        });
                                    });
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(cv.Education))
                            {
                                BuildSectionHeader("Education");
                                var eduEntries = cv.Education.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
                                foreach (var entry in eduEntries)
                                {
                                    col.Item().PaddingTop(10).Row(rowEdu =>
                                    {
                                        rowEdu.ConstantItem(15).Layers(layers =>
                                        {
                                            layers.PrimaryLayer().AlignCenter().LineVertical(0.5f).LineColor(accentColor);
                                            layers.Layer().AlignCenter().PaddingTop(4).Component(new TimePoint(primaryColor));
                                        });

                                        rowEdu.RelativeItem().PaddingLeft(10).PaddingBottom(10).Column(textCol =>
                                        {
                                            var parts = entry.Split(new[] { ':', ']' }, StringSplitOptions.RemoveEmptyEntries);
                                            if (parts.Length >= 2)
                                            {
                                                var date = parts[0].Replace("[", "").Trim();
                                                var school = parts[1].Trim();
                                                var field = parts.Length > 2 ? string.Join(":", parts.Skip(2)).Trim() : "";

                                                textCol.Item().Text(school).FontSize(11).Bold();
                                                textCol.Item().Text(date).FontSize(8).SemiBold().FontColor(primaryColor);
                                                if (!string.IsNullOrEmpty(field))
                                                    textCol.Item().PaddingTop(3).Text(field).FontSize(9).FontColor(Colors.Grey.Darken2);
                                            }
                                            else textCol.Item().Text(entry.Trim()).FontSize(9);
                                        });
                                    });
                                }
                            }

                            if (!string.IsNullOrEmpty(cv.Portfolio))
                            {
                                BuildSectionHeader("Portfolio / Links");
                                col.Item().PaddingTop(8).Column(linkCol =>
                                {
                                    var links = cv.Portfolio.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                                    foreach (var link in links)
                                    {
                                        linkCol.Item().Text(link.Trim()).FontSize(9).FontColor(textColor);
                                    }
                                });
                            }

                            col.Item().AlignBottom().PaddingTop(30).Text("I hereby give consent for my personal data to be processed for the purpose of the recruitment process.").FontSize(7).FontColor(Colors.Grey.Medium).Italic();
                        });
                    });

                    page.Footer().PaddingHorizontal(0).PaddingBottom(10).Row(row =>
                    {
                        row.ConstantItem(180);

                        row.RelativeItem().PaddingHorizontal(35).Row(footerRow =>
                        {
                            footerRow.RelativeItem().AlignLeft().Text("Generated by Recruit Finder AI")
                                .FontSize(8)
                                .FontColor(mutedText);

                            footerRow.RelativeItem().AlignRight().Text(x =>
                            {
                                x.Span("Page ").FontSize(8).FontColor(mutedText);
                                x.CurrentPageNumber().FontSize(8).Bold().FontColor(mutedText);
                            });
                        });
                    });
                });
            });
        }

        private class TimePoint : IComponent
        {
            private readonly string _color;
            public TimePoint(string color) => _color = color;

            public void Compose(IContainer container)
            {
                string svgKropka = $@"
                <svg height='10' width='10'>
                  <circle cx='5' cy='5' r='3.5' fill='white' />
                  <circle cx='5' cy='5' r='2' fill='{_color}' />
                </svg>";

                container.Width(10).Height(10).Svg(svgKropka);
            }
        }
    }
}