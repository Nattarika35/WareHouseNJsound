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
using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.SignalR;
using WareHouseNJsound.Hubs;
using ClosedXML.Excel;
using System.IO;
using System.Threading;

namespace WareHouseNJsound.Controllers
{
    public class RequestController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly CoreContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration Configuration;
        private readonly IHubContext<NotificationHub> _hub;

        public RequestController(ILogger<HomeController> logger, CoreContext context, IHttpContextAccessor httpContextAccessor, IConfiguration configuration, IHubContext<NotificationHub> hub)
        {
            _logger = logger;
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            Configuration = configuration;
            _hub = hub;
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
            var model = new RequestViewModel
            {
                Request = new Request
                {
                    // ถ้าเซิร์ฟเวอร์เป็น UTC แนะนำใช้ DateTime.UtcNow แล้วค่อยแปลง timezone ฝั่งแสดงผล
                    Request_Date = DateTime.Now
                },
                RequestDetails = new List<RequestDetail> { new RequestDetail() }
            };

            // พนักงาน (เฉพาะ Role 202)
            model.Employees = await _context.Employees
                .AsNoTracking()
                .Where(e => e.Role_ID == 202)
                .OrderBy(e => e.Emp_Fname)
                .ToListAsync();

            // งาน
            ViewBag.Jobs = await _context.Jobs
                .AsNoTracking()
                .OrderBy(j => j.JobsName)
                .ToListAsync();

            // สร้าง options สำหรับ <option> เดียวจบ พร้อมหน่วย และคงเหลือ
            // หมายเหตุ: ถ้าชื่อ DbSet เป็น _context.Materials (M ใหญ่) ให้แก้ให้ตรง
            var options = await _context.materials
    .AsNoTracking()
    .Include(m => m.Unit)
    .Select(m => new MaterialOptionDto
    {
        Materials_ID = m.Materials_ID,
        MaterialsName = m.MaterialsName,
        Unit_ID = m.Unit_ID,
        UnitName = m.Unit != null ? m.Unit.UnitName : null,
        StockLeft = _context.Stocks
            .Where(s => s.Materials_ID == m.Materials_ID)
            .Sum(s => (int?)s.OnHandStock) ?? 0
    })
    .OrderBy(x => x.MaterialsName)
    .ToListAsync();

            ViewBag.Materials = options; // หรือใส่ลงใน model.Property ก็ยิ่งดี
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

        public async Task<IActionResult> Create2()
        {
            var model = new RequestViewModel
            {
                Request = new Request
                {
                    // ถ้าเซิร์ฟเวอร์เป็น UTC แนะนำใช้ DateTime.UtcNow แล้วค่อยแปลง timezone ฝั่งแสดงผล
                    Request_Date = DateTime.Now
                },
                RequestDetails = new List<RequestDetail> { new RequestDetail() }
            };

            // พนักงาน (เฉพาะ Role 202)
            model.Employees = await _context.Employees
                .AsNoTracking()
                .Where(e => e.Role_ID == 202)
                .OrderBy(e => e.Emp_Fname)
                .ToListAsync();

            // งาน
            ViewBag.Jobs = await _context.Jobs
                .AsNoTracking()
                .OrderBy(j => j.JobsName)
                .ToListAsync();

            // สร้าง options สำหรับ <option> เดียวจบ พร้อมหน่วย และคงเหลือ
            // หมายเหตุ: ถ้าชื่อ DbSet เป็น _context.Materials (M ใหญ่) ให้แก้ให้ตรง
            var options = await _context.materials
    .AsNoTracking()
    .Include(m => m.Unit)
    .Select(m => new MaterialOptionDto
    {
        Materials_ID = m.Materials_ID,
        MaterialsName = m.MaterialsName,
        Unit_ID = m.Unit_ID,
        UnitName = m.Unit != null ? m.Unit.UnitName : null,
        StockLeft = _context.Stocks
            .Where(s => s.Materials_ID == m.Materials_ID)
            .Sum(s => (int?)s.OnHandStock) ?? 0
    })
    .OrderBy(x => x.MaterialsName)
    .ToListAsync();

            ViewBag.Materials = options; // หรือใส่ลงใน model.Property ก็ยิ่งดี
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Create2(RequestViewModel model)
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
                await LoadNotificationsForTopBarAsync(); // โหลดการแจ้งเตือนด้านบน

                var requests = await _context.Requests
                    .Include(x => x.Status)
                    //.Include(x => x.Workflows).ThenInclude(w => w.Status)
                    .ToListAsync();

                // ✅ ส่งรายการสถานะทั้งหมดไปที่ View
                ViewBag.Statuses = await _context.status
                    .OrderBy(s => s.StatusName)
                    .ToListAsync();

                return View(requests);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                // ส่ง ViewBag.Statuses ว่างไปด้วย กัน View error
                ViewBag.Statuses = new List<Status>();
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
            var request = await _context.Requests
                .Include(r => r.RequestDetails)
                    .ThenInclude(d => d.Materials)
                        .ThenInclude(m => m.Stock)
                .FirstOrDefaultAsync(r => r.Request_ID == requestId);

            if (request == null) return NotFound();

            if (request.Status_ID == 302 || request.Status_ID == 303)
            {
                TempData["ErrorMessage"] = "รายการนี้ถูกปิดสถานะแล้ว";
                return RedirectToAction("PendingRequets", "Request");
            }

            if (decision == "approve")
            {
                // 1) ตรวจสต๊อกก่อน
                foreach (var d in request.RequestDetails)
                {
                    var onhand = d.Materials?.Stock?.OnHandStock ?? 0;
                    if (onhand < d.Quantity)
                    {
                        TempData["ErrorMessage"] =
                            $"สต๊อกไม่พอ: {d.Materials?.MaterialsName} (คงเหลือ {onhand}, ขอ {d.Quantity})";
                        return RedirectToAction("Details", "Request", new { id = requestId });
                    }
                    if (d.Materials == null)
                        throw new InvalidOperationException("พบรายการที่ไม่มีข้อมูล Materials");
                }

                await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                try
                {
                    var now = DateTime.Now;
                    var transList = new List<Transaction>();

                    foreach (var d in request.RequestDetails)
                    {
                        // 2) สร้าง Stock row ถ้ายังไม่มี
                        if (d.Materials!.Stock == null)
                        {
                            d.Materials.Stock = new Stock
                            {
                                Materials_ID = d.Materials_ID!,
                                OnHandStock = 0
                            };
                            _context.Stocks.Add(d.Materials.Stock);
                        }

                        // 3) หักสต๊อก
                        d.Materials.Stock.OnHandStock -= d.Quantity;
                        if (d.Materials.Stock.OnHandStock < 0)
                            throw new InvalidOperationException($"สต๊อกติดลบสำหรับ {d.Materials.MaterialsName}");

                        // 4) Transaction (TranType = 2 = เบิกออก)
                        transList.Add(new Transaction
                        {
                            Transaction_Date = now,
                            TranType_ID = 2,
                            Quantity = d.Quantity,
                            Materials_ID = d.Materials_ID!,
                            Request_ID = request.Request_ID,
                            RequestNumber = request.RequestNumber,
                            Employee_ID = request.Employee_ID,
                            Description = $"จ่ายจากคำขอ {request.RequestNumber}"
                        });
                    }

                    if (transList.Count > 0)
                        _context.Transactions.AddRange(transList);

                    // 5) ปิดสถานะ
                    request.Status_ID = 302;

                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();


                    var date = DateTime.Now;
                    var link = Url.Action("Details", "Edit", new { id = request.Request_ID }, Request.Scheme);
                    var title = "อัปเดตสถานะใบเบิก";
                    var message = decision == "approve"
                        ? $"ใบเบิก {request.RequestNumber} เสร็จสิ้น ✅"
                        : $"ใบเบิก {request.RequestNumber} ยกเลิก ❌";

                    // 1) ดึงรายชื่อแอดมิน (Role_ID == 201)
                    var adminIds = await _context.Employees
                        .Where(e => e.Role_ID == 201)
                        .Select(e => e.Employee_ID)
                        .ToListAsync();

                    // 2) บันทึก Notification ให้ “ทุกแอดมิน” เพื่อให้ badge/รายการใน dropdown ของแต่ละคนถูกต้อง
                    var notis = adminIds.Select(id => new Notification
                    {
                        Employee_ID = id,
                        Title = title,
                        Message = message,
                        Link = link,
                        CreatedAt = now,
                        IsRead = false
                    }).ToList();

                    _context.Notifications.AddRange(notis);
                    await _context.SaveChangesAsync();

                    // 3) ส่ง real-time ไป “กลุ่มแอดมิน” ที่ออนไลน์อยู่
                    await _hub.Clients.Group("Admins").SendAsync("notify", new
                    {
                        title,
                        message,
                        link,
                        createdAt = now
                    });


                    TempData["SuccessMessage"] = "ทำรายการสำเร็จ";
                    return RedirectToAction("PendingRequets", "Request");
                }
                catch (Exception ex)
                {
                    await tx.RollbackAsync();
                    var root = ex.GetBaseException()?.Message ?? ex.Message;
                    TempData["ErrorMessage"] = "ไม่สามารถทำรายการได้: " + root;
                    return RedirectToAction("Details", "Request", new { id = requestId });
                }
            }
            else if (decision == "reject")
            {
                // ยกเลิก
                request.Status_ID = 303;
                // TODO: บันทึกเหตุผล rejectReason ถ้ามีฟิลด์รองรับ
                await _context.SaveChangesAsync();

                //  แจ้งเตือนกรณียกเลิก
                var noti = new Notification
                {
                    Employee_ID = request.Employee_ID,
                    Title = "อัปเดตสถานะใบเบิก",
                    Message = $"ใบเบิก {request.RequestNumber} ยกเลิก ❌",
                    CreatedAt = DateTime.Now,
                    IsRead = false
                };
                _context.Notifications.Add(noti);
                await _context.SaveChangesAsync();

                await _hub.Clients.User(request.Employee_ID)
                          .SendAsync("notify", new
                          {
                              title = noti.Title,
                              message = noti.Message,
                              link = Url.Action("Details", "Request", new { id = request.Request_ID }),
                              createdAt = noti.CreatedAt
                          });

                TempData["SuccessMessage"] = "ทำรายการสำเร็จ";
                return RedirectToAction("PendingRequets", "Request");
            }

            TempData["ErrorMessage"] = "คำสั่งไม่ถูกต้อง";
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
        public async Task<IActionResult> Dashboard()
        {
            // การ์ดสรุป
            var totalSku = await _context.materials.CountAsync();
            var totalOnHand = await _context.Stocks.SumAsync(s => (int?)s.OnHandStock) ?? 0;
            var lowStockCount = await _context.materials
                .Include(m => m.Stock)
                .Where(m => m.MinimumStock > 0 && (m.Stock != null && m.Stock.OnHandStock < m.MinimumStock))
                .CountAsync();

            // ถ้ามีคำร้องรออนุมัติ (ปรับตามระบบของคุณ)
            var pending = await _context.Requests.CountAsync(r => r.Status_ID == 301 /* Pending */);

            // คงเหลือต่ำกว่า Minimum (Top 10)
            var lowStocks = await _context.materials
                .AsNoTracking()
                .Include(m => m.Stock)
                .Include(m => m.Unit)
                .Where(m => m.MinimumStock > 0 && (m.Stock != null && m.Stock.OnHandStock < m.MinimumStock))
                .OrderBy(m => (m.Stock!.OnHandStock * 1.0) / m.MinimumStock) // สัดส่วนคงเหลือ/ขั้นต่ำ
                .ThenBy(m => m.MaterialsName)
                .Take(10)
                .Select(m => new LowStockRow
                {
                    Materials_ID = m.Materials_ID,
                    MaterialsName = m.MaterialsName,
                    UnitName = m.Unit != null ? m.Unit.UnitName : null,
                    OnHand = m.Stock != null ? (m.Stock.OnHandStock ?? 0) : 0,
                    MinimumStock = m.MinimumStock ?? 0
                })
                .ToListAsync();

            // รายการรับเข้า (TranType=1) ล่าสุด 10 รายการ
            var receipts = await _context.Transactions
                .AsNoTracking()
                .Where(t => t.TranType_ID == 1)
                .OrderByDescending(t => t.Transaction_Date)
                .Take(10)
                .Join(_context.materials,
                      t => t.Materials_ID,
                      m => m.Materials_ID,
                      (t, m) => new { t, m })
                .Join(_context.Units,  // ถ้า Unit เป็น nav ของ Materials อยู่แล้ว จะ select จาก m.Unit ได้เหมือนกัน
                      tm => tm.m.Unit_ID,
                      u => u.Unit_ID,
                      (tm, u) => new TransRow
                      {
                          Date = tm.t.Transaction_Date,
                          Materials_ID = tm.m.Materials_ID,
                          MaterialsName = tm.m.MaterialsName,
                          Qty = tm.t.Quantity ?? 0,
                          UnitName = u.UnitName,
                          DocNo = tm.t.RequestNumber,
                          Employee_ID = tm.t.Employee_ID,
                          Description = tm.t.Description
                      })
                .ToListAsync();

            // รายการเบิกออก (TranType=2) ล่าสุด 10 รายการ
            var issues = await _context.Transactions
                .AsNoTracking()
                .Where(t => t.TranType_ID == 2)
                .OrderByDescending(t => t.Transaction_Date)
                .Take(10)
                .Join(_context.materials,
                      t => t.Materials_ID,
                      m => m.Materials_ID,
                      (t, m) => new { t, m })
                .Join(_context.Units,
                      tm => tm.m.Unit_ID,
                      u => u.Unit_ID,
                      (tm, u) => new TransRow
                      {
                          Date = tm.t.Transaction_Date,
                          Materials_ID = tm.m.Materials_ID,
                          MaterialsName = tm.m.MaterialsName,
                          Qty = tm.t.Quantity ?? 0,
                          UnitName = u.UnitName,
                          DocNo = tm.t.RequestNumber,
                          Employee_ID = tm.t.Employee_ID,
                          Description = tm.t.Description
                      })
                .ToListAsync();

            var vm = new DashboardVM
            {
                TotalSku = totalSku,
                TotalOnHand = totalOnHand,
                LowStockCount = lowStockCount,
                PendingRequests = pending,
                LowStocks = lowStocks,
                RecentReceipts = receipts,
                RecentIssues = issues
            };

            return View(vm);
        }


        [HttpGet]
        public async Task<IActionResult> ExportLowStocks(CancellationToken ct)
        {
            // ดึงข้อมูล “ต่ำกว่า Minimum” แบบเดียวกับใน Dashboard
            var rows = await _context.materials
                .AsNoTracking()
                .Include(m => m.Stock)
                .Include(m => m.Unit)
                .Where(m => m.MinimumStock > 0 && (m.Stock != null && m.Stock.OnHandStock < m.MinimumStock))
                .OrderBy(m => (m.Stock!.OnHandStock * 1.0) / m.MinimumStock)
                .ThenBy(m => m.MaterialsName)
                .Select(m => new
                {
                    Materials_ID = m.Materials_ID,
                    MaterialsName = m.MaterialsName,
                    UnitName = m.Unit != null ? m.Unit.UnitName : null,
                    OnHand = m.Stock != null ? (m.Stock.OnHandStock ?? 0) : 0,
                    MinimumStock = m.MinimumStock ?? 0,

                })
                .ToListAsync(ct);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("LowStock");

            // สร้างตารางจาก IEnumerable โดยมีหัวคอลัมน์อัตโนมัติ
            var table = ws.Cell(1, 1).InsertTable(rows, true);
            table.Theme = XLTableTheme.TableStyleMedium9;

            // ปรับหัวคอลัมน์ให้อ่านง่าย (ถ้าต้องการชื่อไทย)
            ws.Cell(1, 1).Value = "รหัสวัสดุ";
            ws.Cell(1, 2).Value = "ชื่อวัสดุ";
            ws.Cell(1, 3).Value = "หน่วย";
            ws.Cell(1, 4).Value = "คงเหลือ";
            ws.Cell(1, 5).Value = "ขั้นต่ำ";

            // จัดรูปแบบ
            ws.Columns().AdjustToContents();
            ws.Column(6).Style.NumberFormat.Format = "0.00";

            // แช่หัวคอลัมน์
            ws.SheetView.FreezeRows(1);

            // ชื่อไฟล์: LowStock-yyyyMMdd-HHmm.xlsx (โซนเวลาไทย)
            var now = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "SE Asia Standard Time");
            var fileName = $"LowStock-{now:yyyyMMdd-HHmm}.xlsx";

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            ms.Position = 0;

            return File(
                fileContents: ms.ToArray(),
                contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileDownloadName: fileName
            );
        }



        public async Task<IActionResult> TopMaterialsChart(
                [Range(1, 50)] int top = 5,
                DateTime? start = null,   // รับช่วงวันที่ (ไม่บังคับ)
                DateTime? end = null
            )
        {
            var q = _context.RequestDetails
                .AsNoTracking()
                .Include(d => d.Materials)
                .Include(d => d.Request)
                .Where(d => d.Request != null && d.Request.Status_ID == 302) // เฉพาะที่เสร็จสิ้น
                .Where(d => d.Materials != null && d.Materials.MaterialsName != null);

            // กรองช่วงวันที่ ถ้าส่งมา (สมมติใช้วันที่ของใบคำร้อง)
            if (start.HasValue)
                q = q.Where(d => d.Request.Request_Date >= start.Value.Date);
            if (end.HasValue)
                q = q.Where(d => d.Request.Request_Date < end.Value.Date.AddDays(1)); // รวมทั้งวัน

            var data = await q
                .GroupBy(d => d.Materials.MaterialsName)
                .Select(g => new
                {
                    MaterialName = g.Key,
                    TotalQty = g.Sum(x => x.Quantity)
                })
                .OrderByDescending(x => x.TotalQty)
                .Take(top)
                .ToListAsync();

            var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            return new JsonResult(data, opts);
        }

        public class DashboardVM
        {
            // การ์ดสรุป
            public int TotalSku { get; set; }            // จำนวนรายการสินค้า (SKU)
            public int TotalOnHand { get; set; }         // จำนวนคงเหลือรวมทั้งหมด
            public int LowStockCount { get; set; }       // จำนวนสินค้าที่ต่ำกว่าขั้นต่ำ
            public int PendingRequests { get; set; }     // (ถ้ามี workflow รออนุมัติ)

            // Alert: คงเหลือต่ำกว่า Min
            public List<LowStockRow> LowStocks { get; set; } = new();

            // รายการเคลื่อนไหวล่าสุด
            public List<TransRow> RecentReceipts { get; set; } = new(); // TranType=1
            public List<TransRow> RecentIssues { get; set; } = new();   // TranType=2
        }

        public class LowStockRow
        {
            public string Materials_ID { get; set; } = default!;
            public string MaterialsName { get; set; } = default!;
            public string UnitName { get; set; }
            public int OnHand { get; set; }
            public int MinimumStock { get; set; }
        }

        public class TransRow
        {
            public DateTime Date { get; set; }
            public string Materials_ID { get; set; } = default!;
            public string MaterialsName { get; set; } = default!;
            public int Qty { get; set; }
            public string UnitName { get; set; }
            public string DocNo { get; set; }      // ใช้ RequestNumber/เอกสารอ้างอิง
            public string Employee_ID { get; set; }
            public string Description { get; set; }
        }

        // 1. การ์ดสรุป
        [HttpGet]
        public async Task<IActionResult> DashboardCounts()
        {
            var totalSku = await _context.materials.CountAsync();
            var totalOnHand = await _context.Stocks.SumAsync(s => (int?)s.OnHandStock) ?? 0;
            var lowStockCount = await _context.materials
                .Include(m => m.Stock)
                .CountAsync(m => m.MinimumStock > 0 && m.Stock != null && m.Stock.OnHandStock < m.MinimumStock);
            var pending = await _context.Requests.CountAsync(r => r.Status_ID == 301);

            return Json(new { totalSku, totalOnHand, lowStockCount, pending });
        }

        // 2. Low stock (Top 10)
        [HttpGet]
        public async Task<IActionResult> LowStockTable()
        {
            var rows = await _context.materials
                .AsNoTracking()
                .Include(m => m.Stock)
                .Include(m => m.Unit)
                .Where(m => m.MinimumStock > 0 && m.Stock != null && m.Stock.OnHandStock < m.MinimumStock)
                .OrderBy(m => (m.Stock!.OnHandStock * 1.0) / m.MinimumStock)
                .ThenBy(m => m.MaterialsName)
                .Take(10)
                .Select(m => new
                {
                    m.Materials_ID,
                    m.MaterialsName,
                    UnitName = m.Unit != null ? m.Unit.UnitName : null,
                    OnHand = m.Stock != null ? m.Stock.OnHandStock : 0,
                    m.MinimumStock
                })
                .ToListAsync();

            return PartialView("_LowStockTable", rows);
        }

        // 3. เคลื่อนไหวล่าสุด (รับเข้า/เบิกออก)
        [HttpGet]
        public async Task<IActionResult> RecentTransactions(int type = 1, int take = 10)
        {
            var list = await _context.Transactions
                .AsNoTracking()
                .Where(t => t.TranType_ID == type)
                .OrderByDescending(t => t.Transaction_Date)
                .Take(take)
                .Join(_context.materials, t => t.Materials_ID, m => m.Materials_ID, (t, m) => new
                {
                    t.Transaction_Date,
                    t.Quantity,
                    t.RequestNumber,
                    t.Employee_ID,
                    t.Description,
                    m.Materials_ID,
                    m.MaterialsName,
                    UnitName = m.Unit.UnitName
                })
                .ToListAsync();

            return PartialView("_RecentTransTable", list);
        }

        private async Task CreateNotiAndPushAsync(string employeeId, string title, string message, string link = null)
        {
            var noti = new Notification
            {
                Employee_ID = employeeId,
                Title = title,
                Message = message,
                Link = link
            };
            _context.Notifications.Add(noti);
            await _context.SaveChangesAsync();

            // ส่ง real-time ไปหา user คนนั้น (ต้องตั้ง Claim NameIdentifier = Employee_ID)
            await _hub.Clients.User(employeeId).SendAsync("notify", new
            {
                title,
                message,
                link,
                createdAt = noti.CreatedAt
            });
        }
    }
}
