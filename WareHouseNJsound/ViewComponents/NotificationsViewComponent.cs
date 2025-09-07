using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WareHouseNJsound.Data;

namespace WareHouseNJsound.ViewComponents
{
    public class NotificationsViewComponent : ViewComponent
    {
        private readonly CoreContext _context;
        private readonly IHttpContextAccessor _http;

        public NotificationsViewComponent(CoreContext context, IHttpContextAccessor http)
        {
            _context = context;
            _http = http;
        }

        // ===== ViewModel (nested) =====
        public class NotificationBellVM
        {
            public int Count { get; set; }
            public List<Item> Items { get; set; } = new();
            public class Item
            {
                public int Id { get; set; }
                public string Title { get; set; }
                public string Message { get; set; }
                public DateTime CreatedAt { get; set; }
                public string Link { get; set; }
            }
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var empId = _http.HttpContext?.Session.GetString("Employee_ID");

            var q = _context.Notifications
                            .AsNoTracking()
                            .Where(n => n.Employee_ID == empId && !n.IsRead)
                            .OrderByDescending(n => n.CreatedAt)
                            .Take(10);

            var list = await q.Select(n => new NotificationBellVM.Item
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Message,
                CreatedAt = n.CreatedAt,
                Link = n.Link
            }).ToListAsync();

            var vm = new NotificationBellVM
            {
                Count = list.Count,
                Items = list
            };

            return View(vm); // จะไปหา Views/Shared/Components/Notifications/Default.cshtml
        }
    }
}
