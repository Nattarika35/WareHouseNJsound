using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

namespace WareHouseNJsound.Hubs
{
    public class NotificationHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var roleId = Context.User?.FindFirst("RoleId")?.Value;
            var isAdmin = roleId == "201" || (Context.User?.IsInRole("Admin") ?? false);

            if (isAdmin)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
            }

            // ช่วย debug
            Console.WriteLine($"[Hub] Conn={Context.ConnectionId}, UserIdentifier={Context.UserIdentifier}, IsAdmin={isAdmin}");
            await base.OnConnectedAsync();
        }

        // (เผื่อใช้ client เรียก join เอง)
        public async Task JoinGroup(string groupName)
        {
            // กันคนไม่ใช่แอดมินเข้ากลุ่ม Admins
            if (groupName == "Admins")
            {
                var roleId = Context.User?.FindFirst("RoleId")?.Value;
                var isAdmin = roleId == "201" || (Context.User?.IsInRole("Admin") ?? false);
                if (!isAdmin) return;
            }
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }
    }
}
