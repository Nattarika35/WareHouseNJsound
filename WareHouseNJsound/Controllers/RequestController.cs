using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using WareHouseNJsound.Data;
using WareHouseNJsound.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Threading.Tasks;

namespace WareHouseNJsound.Controllers
{
    public class RequestController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly CoreContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration Configuration;

        public RequestController(ILogger<HomeController> logger, CoreContext context, IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
        {
            _logger = logger;
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            Configuration = configuration;

        }
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<JsonResult> GetEmployeeName(string employeeId)
        {
            if (string.IsNullOrEmpty(employeeId))
                return Json("");

            var employee = await _context.Employees
                                         .FirstOrDefaultAsync(e => e.Employee_ID == employeeId);

            if (employee != null)
                return Json(employee.FullName);
            else
                return Json("");
        }


        public async Task<IActionResult> Create()
        {
            var model = new RequestViewModel();
            model.Request = new Request();

            // วันที่ปัจจุบัน
            model.Request.Request_Date = DateTime.Now;         

            // ดึง Employee ทั้งหมดจาก DB
            model.Employees = await _context.Employees
                                            .Where(e => e.Role_ID == 202)
                                            .AsNoTracking()
                                            .OrderBy(e => e.Emp_Fname)
                                            .ToListAsync();

            // ดึง Jobs
            var jobs = await _context.Jobs
                                     .AsNoTracking()
                                     .OrderBy(j => j.JobsName)
                                     .ToListAsync();

            ViewBag.Jobs = jobs;  //  โยนค่าไปที่ View

            // ดึง Materials พร้อม Unit
            var materials = _context.materials
                        .Include(m => m.Unit)
                        .ToList();
            ViewBag.Materials = materials ?? new List<Materials>();
            //  โยนไป View

            // กัน null
            model.RequestDetails = new List<RequestDetail>
    {
        new RequestDetail()
    };

            return View(model);
        }


        [HttpPost]
        public async Task<IActionResult> Create(RequestViewModel model)
        {
            model.Request ??= new Request();
            model.RequestDetails ??= new List<RequestDetail>();

            if (!ModelState.IsValid)
            {
                // โหลดข้อมูลกลับไป view เหมือนเดิม...
                // (ข้ามรายละเอียดซ้ำ)
                return View(model);
            }

            model.Request.Request_ID = Guid.NewGuid();
            model.Request.Request_Date = DateTime.Now;
            string guidPart = Guid.NewGuid().ToString("N").Substring(0, 2).ToUpper();
            model.Request.RequestNumber = $"REQ-{DateTime.Now:yyyyMMdd}{guidPart}";
            model.Request.Status_ID = 301;

            _context.Requests.Add(model.Request);
            await _context.SaveChangesAsync();

            foreach (var detail in model.RequestDetails)
            {
                if (string.IsNullOrEmpty(detail.Materials_ID) || detail.Quantity <= 0)
                    continue;

                // Fallback: ถ้า Unit_ID == 0 ให้ดึงจากวัสดุ
                if (detail.Unit_ID == 0)
                {
                    detail.Unit_ID = await _context.materials
                        .Where(m => m.Materials_ID == detail.Materials_ID)
                        .Select(m => m.Unit_ID)
                        .FirstOrDefaultAsync();
                }

                detail.RequestDetail_ID = Guid.NewGuid();
                detail.Request_ID = model.Request.Request_ID;
                _context.RequestDetails.Add(detail);
            }
            // ลิงก์ไปหน้าอนุมัติ/ดูรายละเอียดคำขอ
            var linkToEdit = Url.Action("Edit", "Request", new { requestId = model.Request.Request_ID });

            // ดึงรายชื่อแอดมิน
            var admins = await _context.Employees
                .Where(e => e.Role_ID == 201) // 201 = Admin
                .Select(e => e.Employee_ID)
                .ToListAsync();

            // สร้างแจ้งเตือนให้ทุกแอดมิน
            foreach (var adminId in admins)
            {
                _context.Notifications.Add(new Notification
                {
                    Employee_ID = adminId,
                    Title = "มีคำขอใหม่",
                    Message = $"คำขอเลขที่ {model.Request.RequestNumber} รออนุมัติ",
                    Link = linkToEdit
                });
            }

            await _context.SaveChangesAsync();


            // ✅ ใช้ TempData แล้ว Redirect ไปหน้ารออนุมัติ
            TempData["SuccessMessage"] = "บันทึกใบเบิกสำเร็จ";
            TempData["RequestNumber"] = model.Request.RequestNumber;
            return RedirectToAction("PendingRequets", "Request");
        }



        public async Task<string> CheckdataRequestIncontroller()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(Configuration.GetConnectionString("WarehouseAndStockConnection")))
                {
                    using (SqlCommand command = new SqlCommand("spRunningRequestNumber", connection))
                    {
                        await connection.OpenAsync();
                        command.CommandType = CommandType.StoredProcedure;

                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                string newRequestNumber = reader["NewRequestNumber"] != DBNull.Value ? reader["NewRequestNumber"].ToString() : string.Empty;

                                return newRequestNumber;
                            }
                            else
                            {
                                return "No data found";
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }

        public async Task<IActionResult> PendingRequets()
        {
            try
            {
                await LoadNotificationsForTopBarAsync(); // << ใส่ตรงนี้

                var requests = await _context.Requests
                    .Include(x => x.Status)
                    //.Include(x => x.Workflows)
                    //    .ThenInclude(w => w.Status)
                    .ToListAsync();
                return View(requests);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return View(new List<Request>());

            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(Guid requestId)
        {
            await LoadNotificationsForTopBarAsync(); // << ใส่ตรงนี้

            var req = await _context.Requests
                .Include(r => r.Employee) // ถ้า navigation ชื่อ Employee ให้เปลี่ยนเป็น .Include(r => r.Employee)
                .Include(r => r.RequestDetails).ThenInclude(d => d.Materials)
                .Include(r => r.RequestDetails).ThenInclude(d => d.Unit)
                .Include(r => r.RequestDetails).ThenInclude(d => d.Jobs)
                .FirstOrDefaultAsync(r => r.Request_ID == requestId);

            if (req == null) return NotFound();

            return View(req); // ส่ง Entity ตรง ๆ (มี navigation ครบ)
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStatus(Guid requestId, string decision, string rejectReason)
        {
            var req = await _context.Requests.FirstOrDefaultAsync(r => r.Request_ID == requestId);
            if (req == null) return NotFound();

            // 👉 ปรับส่วนนี้ให้ตรง schema ของคุณ
            // ตัวอย่างที่ 1: ถ้าใน Request มีฟิลด์ Status_ID (int)
            // 1 = Pending, 2 = Approved, 3 = Rejected (ตัวเลขเป็นเพียงตัวอย่าง)
            if (decision == "approve")
            {
                // req.Status_ID = 2;
                // ถ้ามีฟิลด์อื่นประกอบ เช่น ApprovedBy / ApprovedDate ก็เซ็ตที่นี่
            }
            else if (decision == "reject")
            {
                // req.Status_ID = 3;
                // ถ้ามีฟิลด์ Reason/Remark เก็บสาเหตุ ให้บันทึก rejectReason
                // req.RejectReason = rejectReason;
            }

            // ตัวอย่างที่ 2: ถ้าคุณใช้ Workflow/Status แยกตาราง
            // ให้ไปเพิ่มแถว Workflow ใหม่ หรืออัปเดต CurrentStatus ตามระบบของคุณแทน

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = decision == "approve"
                ? "อนุมัติคำร้องเรียบร้อย"
                : "ปฏิเสธคำร้องเรียบร้อย";
            TempData["RequestNumber"] = req.RequestNumber;

            return RedirectToAction("PendingRequets", "Request");
        }

        private async Task LoadNotificationsForTopBarAsync()
        {
            var roleId = HttpContext.Session.GetInt32("Role_ID");
            var empId = HttpContext.Session.GetString("Employee_ID");

            if (roleId == 201 && !string.IsNullOrEmpty(empId))
            {
                ViewBag.NotificationCount = await _context.Notifications
                    .CountAsync(n => n.Employee_ID == empId && !n.IsRead);

                ViewBag.Notifications = await _context.Notifications
                    .Where(n => n.Employee_ID == empId)
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(5)
                    .ToListAsync();
            }
            else
            {
                ViewBag.NotificationCount = 0;
                ViewBag.Notifications = new List<Notification>();
            }
        }


    }
}
