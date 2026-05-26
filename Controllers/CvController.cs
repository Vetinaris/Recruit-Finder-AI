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
using System.Text;
using System.Text.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;

namespace Recruit_Finder_AI.Controllers
{
    [Authorize]
    public class CvController : Controller
    {
        private readonly Recruit_Finder_AIContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public CvController(Recruit_Finder_AIContext context,
                            UserManager<ApplicationUser> userManager,
                            IHttpClientFactory httpClientFactory,
                            IConfiguration configuration)
        {
            _context = context;
            _userManager = userManager;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> GetStatus(int id)
        {
            var userId = _userManager.GetUserId(User);
            var cv = await _context.Cvs
                .Where(c => c.Id == id && c.UserId == userId)
                .Select(c => new { c.IsVerified })
                .FirstOrDefaultAsync();

            if (cv == null) return NotFound();
            return Json(cv);
        }

        [AllowAnonymous]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> UpdateAiResult([FromBody] JsonElement result)
        {
            var apiKey = _configuration["AiService:ApiKey"];
            if (!Request.Headers.TryGetValue("X-AI-Key", out var extractedKey) || extractedKey != apiKey)
            {
                return Unauthorized("Invalid API Key");
            }

            try
            {
                int cvId = result.GetProperty("cvId").GetInt32();
                var cv = await _context.Cvs.FindAsync(cvId);

                if (cv != null)
                {
                    cv.IsVerified = true;
                    _context.Update(cv);

                    var notification = new Notification
                    {
                        UserId = cv.UserId,
                        Title = "AI Verification Complete",
                        Content = $"AI Verification for your CV is complete!",
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false,
                        IsCompleted = true,
                        ActionUrl = Url.Action("Index", "Cv")
                    };
                    _context.Add(notification);
                    await _context.SaveChangesAsync();
                }
                return Ok();
            }
            catch { return BadRequest(); }
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
        public async Task<IActionResult> Create([Bind("Name,Surname,IncludePhoto,DateOfBirth,Address,PhoneNumber,Email,ProfessionalExperience,Education,Portfolio,Languages,Skills,Interests")] Cv cv)
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

            _ = SendToAiVerification(cv);

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
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Surname,IncludePhoto,DateOfBirth,Address,PhoneNumber,Email,ProfessionalExperience,Education,Portfolio,Languages,Skills,Interests")] Cv cv)
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

                _context.Update(cv);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "CV updated and re-queued for analysis!";
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
            try
            {
                var aiUrl = _configuration["AiService:PythonServiceUrl"];
                var apiKey = _configuration["AiService:ApiKey"];

                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(5);

                client.DefaultRequestHeaders.Add("X-AI-Key", apiKey);

                var jsonData = JsonSerializer.Serialize(new
                {
                    cvId = cv.Id,
                    fullName = $"{cv.Name} {cv.Surname}",
                    experience = cv.ProfessionalExperience,
                    education = cv.Education,
                    skills = cv.Skills,
                    languages = cv.Languages,
                    portfolio = cv.Portfolio
                });

                var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(aiUrl, content);
                return response.IsSuccessStatusCode;
            }
            catch
            {
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
            var sidebarBackgroundColor = Colors.Grey.Lighten5;
            var user = _context.Users.FirstOrDefault(u => u.Id == cv.UserId);
            byte[]? photoData = cv.IncludePhoto ? user?.ProfilePicture : null;

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Verdana).FontColor(textColor));

                    page.Background().Row(row =>
                    {
                        row.ConstantItem(180).Background(sidebarBackgroundColor);
                        row.RelativeItem().Background(Colors.White);
                    });

                    page.Content().Row(row =>
                    {
                        row.ConstantItem(180).Background(Colors.Grey.Lighten5).Padding(20).Column(col =>
                        {
                            if (photoData != null)
                            {
                                col.Item().AlignCenter().Width(120).Height(120).Image(photoData).FitArea();
                            }
                            else
                            {
                                col.Item().AlignCenter().Width(100).Height(100).Background(Colors.Grey.Lighten3);
                            }
                            col.Item().PaddingVertical(10);

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

                    page.Footer().PaddingBottom(10).Row(row =>
                    {
                        row.ConstantItem(180);

                        row.RelativeItem().PaddingHorizontal(35).AlignRight().Text(text =>
                        {
                            text.Span("Generated by ").FontSize(8).FontColor(mutedText);
                            text.Span("Recruit Finder AI").FontSize(8).SemiBold().FontColor(primaryColor);
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