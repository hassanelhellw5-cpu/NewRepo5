using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace QemmaProject.Controllers
{
    internal static class ControllerAuthExtensions
    {
        internal static bool IsSelfOrAdmin(this ControllerBase controller, string? userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return false;
            if (controller.User.IsInRole("Admin")) return true;
            return string.Equals(controller.User.FindFirstValue(ClaimTypes.NameIdentifier), userId, StringComparison.Ordinal);
        }

        internal static IActionResult ForbiddenUser(this ControllerBase controller)
            => controller.Forbid();
    }
}
