using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using WareHouseNJsound.Models;
using WareHouseNJsound.Data;
using Microsoft.EntityFrameworkCore;

namespace WareHouseNJsound.Controllers
{
    public class NotificationsController : Controller
    {
        private readonly CoreContext _context;  // ← ใช้ CoreContext
        public NotificationsController(CoreContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Go(int id)
        {
            var empId = HttpContext.Session.GetString("Employee_ID");
            if (string.IsNullOrEmpty(empId)) return Unauthorized();

            var n = await _context.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.Employee_ID == empId);
            if (n == null) return NotFound();

            n.IsRead = true;
            await _context.SaveChangesAsync();

            return !string.IsNullOrEmpty(n.Link)
                ? Redirect(n.Link)
                : RedirectToAction("Index", "Home");
        }
    }
}
