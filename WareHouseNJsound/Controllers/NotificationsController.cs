using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using WareHouseNJsound.Models;
using WareHouseNJsound.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System;
using Microsoft.AspNetCore.SignalR;
using WareHouseNJsound.Hubs;
using System.Security.Claims;

namespace WareHouseNJsound.Controllers
{
    public class NotificationsController : Controller
    {
        private readonly CoreContext _context;
        private readonly IHubContext<NotificationHub> _hub;

        public NotificationsController(CoreContext context, IHubContext<NotificationHub> hub)
        {
            _context = context;
            _hub = hub;
        }



        private string? GetEmployeeId()
        {
            // 1) Session
            var fromSession = HttpContext.Session.GetString("Employee_ID");
            if (!string.IsNullOrWhiteSpace(fromSession)) return fromSession.Trim();

            // 2) Claims (ถ้ามี)
            var claim = User?.FindFirst("Employee_ID")?.Value
                        ?? User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrWhiteSpace(claim)) return claim.Trim();

            // 3) ชื่อล็อกอิน (สุดท้ายจริง ๆ)
            var name = User?.Identity?.Name;
            return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        }



        // GET: /Notifications
        // หน้าแสดงรายการแจ้งเตือนล่าสุด
        public async Task<IActionResult> Index()
        {
            var empId = GetEmployeeId()?.Trim(); // กัน space
                                                 // ถ้าอยากให้หน้า Index ยังเห็น "ประกาศส่วนกลาง" แม้ empId ว่าง ให้ไม่ return ทันที

            var q = _context.Notifications.AsNoTracking();

            // รองรับ 3 เคส: ของพนักงาน / broadcast = null / broadcast = "all"
            if (!string.IsNullOrEmpty(empId))
            {
                q = q.Where(n =>
                    n.Employee_ID == empId ||
                    n.Employee_ID == null ||
                    n.Employee_ID == "all");
            }
            else
            {
                // ถ้าไม่มี empId ให้แสดงเฉพาะ broadcast
                q = q.Where(n => n.Employee_ID == null || n.Employee_ID == "all");
            }

            var items = await q
                .OrderByDescending(n => n.CreatedAt)
                .Take(50)
                .ToListAsync();

            // (อ็อปชัน) debug ชั่วคราว
            ViewBag.DebugEmpId = empId;
            ViewBag.DebugCount = items.Count;

            return View(items);
        }


        // GET: /Notifications/MyList?since=2025-09-01T00:00:00Z
        // ใช้ดึงรายการใหม่ ๆ เป็น JSON (ถ้าจะทำ polling/refresh)
        [HttpGet]
        public async Task<IActionResult> MyList(DateTime? since = null, int take = 10)
        {
            var empId = GetEmployeeId();
            if (string.IsNullOrEmpty(empId)) return Json(Array.Empty<object>());

            var q = _context.Notifications.Where(n => n.Employee_ID == empId);
            if (since.HasValue)
                q = q.Where(n => n.CreatedAt > since.Value);

            var items = await q.OrderByDescending(n => n.CreatedAt)
                               .Take(Math.Clamp(take, 1, 100))
                               .Select(n => new
                               {
                                   id = n.Id,
                                   title = n.Title,
                                   message = n.Message,
                                   link = n.Link,
                                   createdAt = n.CreatedAt,
                                   isRead = n.IsRead
                               })
                               .ToListAsync();
            return Json(items);
        }

        // POST: /Notifications/MarkAllRead
        // เรียกตอนกดเปิดกระดิ่ง → รีเซ็ต badge
        [HttpPost]
        public async Task<IActionResult> MarkAllRead()
        {
            var empId = GetEmployeeId()?.Trim();
            if (string.IsNullOrEmpty(empId))
                return BadRequest(new { ok = false, error = "EMPID_MISSING" });

            var notifs = await _context.Notifications
                .Where(n => n.Employee_ID == empId && !n.IsRead)
                .ToListAsync();

            foreach (var n in notifs)
            {
                n.IsRead = true;
            }

            await _context.SaveChangesAsync();

            return Ok(new { ok = true, updated = notifs.Count });
        }


        // GET: /Notifications/Go/{id}
        // กดแจ้งเตือนตัวใดตัวหนึ่ง -> mark read แล้วพาไปลิงก์ (ถ้ามี)
        [HttpGet]
        public async Task<IActionResult> Go(int id)
        {
            var empId = GetEmployeeId();
            if (string.IsNullOrEmpty(empId)) return Unauthorized();

            var noti = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.Employee_ID == empId);

            if (noti == null) return NotFound();

            if (!noti.IsRead)
            {
                noti.IsRead = true;
                await _context.SaveChangesAsync();
            }

            if (!string.IsNullOrWhiteSpace(noti.Link))
                return Redirect(noti.Link);

            // ถ้าไม่มีลิงก์ให้กลับหน้ารายการแจ้งเตือน
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> TestPing([FromServices] IHubContext<NotificationHub> hub, string to = "all")
        {
            // จะส่งเป็น string ธรรมดา หรือเป็น object ก็ได้
            var payload = new
            {
                title = "ทดสอบแจ้งเตือน",
                message = $"PING @ {DateTime.Now:HH:mm:ss}",
                link = Url.Action("Index", "Notifications"),
                createdAt = DateTime.Now
            };

            if (string.Equals(to, "all", StringComparison.OrdinalIgnoreCase))
                await hub.Clients.All.SendAsync("notify", payload);
            else
                await hub.Clients.User(to).SendAsync("notify", payload); // ระบุ Employee_ID

            return Content("Sent");
        }

        [HttpGet]
        public async Task<IActionResult> TestAdmin(
        [FromServices] IHubContext<NotificationHub> hub,
        string to = "admins")
            {
                var payload = new
                {
                    title = "ทดสอบแจ้งเตือน",
                    message = $"hello {to}",
                    createdAt = DateTime.Now,
                    link = "#" // หรือ Url.Action(...) ก็ได้
                };

                if (string.Equals(to, "admins", StringComparison.OrdinalIgnoreCase))
                {
                    await hub.Clients.Group("Admins").SendAsync("notify", payload);
                }
                else if (string.Equals(to, "all", StringComparison.OrdinalIgnoreCase))
                {
                    await hub.Clients.All.SendAsync("notify", payload);
                }
                else
                {
                    // สมมติ to = "E001" (Employee_ID)
                    await hub.Clients.User(to).SendAsync("notify", payload);
                }

                return Content($"sent to {to}");
            }

    }
}
