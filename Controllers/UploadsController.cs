using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QemmaProject.Controllers
{
    [Route("api/uploads")]
    [ApiController]
    public class UploadsController : ControllerBase
    {
        private static readonly HashSet<string> AllowedReceiptExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp",
            ".pdf"
        };

        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;

        public UploadsController(IWebHostEnvironment environment, IConfiguration configuration)
        {
            _environment = environment;
            _configuration = configuration;
        }

        [Authorize]
        [HttpPost("receipts")]
        [RequestSizeLimit(5 * 1024 * 1024)]
        public async Task<IActionResult> UploadReceipt(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest(new { message = "Receipt file is required." });
            if (file.Length > 5 * 1024 * 1024) return BadRequest(new { message = "Receipt file cannot exceed 5 MB." });

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedReceiptExtensions.Contains(extension))
            {
                return BadRequest(new { message = "Allowed receipt file types are jpg, jpeg, png, webp, and pdf." });
            }

            var uploadsRoot = Path.Combine(_environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", "receipts");
            Directory.CreateDirectory(uploadsRoot);

            var storedFileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            var filePath = Path.Combine(uploadsRoot, storedFileName);

            await using (var stream = System.IO.File.Create(filePath))
            {
                await file.CopyToAsync(stream);
            }

            var relativeUrl = $"/uploads/receipts/{storedFileName}";
            var publicBaseUrl = (_configuration["PublicBaseUrl"] ?? _configuration["ApiDomain"] ?? string.Empty).TrimEnd('/');
            var absoluteUrl = string.IsNullOrWhiteSpace(publicBaseUrl) ? relativeUrl : $"{publicBaseUrl}{relativeUrl}";

            return Ok(new
            {
                fileName = storedFileName,
                url = relativeUrl,
                absoluteUrl,
                file.Length,
                file.ContentType
            });
        }
    }
}
