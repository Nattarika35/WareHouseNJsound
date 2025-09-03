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

            if (ModelState.IsValid)
            {
                // สร้าง Request_ID
                model.Request.Request_ID = Guid.NewGuid();

                // กำหนดวันที่ปัจจุบัน
                model.Request.Request_Date = DateTime.Now;

                // สร้างเลข RequestNumber ป้องกันซ้ำ
                // ใช้รูปแบบ REQ-yyyyMMddHHmmss + 4 ตัวอักษรจาก Guid
                string guidPart = Guid.NewGuid().ToString("N").Substring(0, 2).ToUpper();
                model.Request.RequestNumber = $"REQ-{DateTime.Now:yyyyMMdd}{guidPart}";

                // บันทึก Request
                _context.Requests.Add(model.Request);
                await _context.SaveChangesAsync();

                // บันทึก RequestDetails
                foreach (var detail in model.RequestDetails)
                {
                    if (!string.IsNullOrEmpty(detail.Materials_ID) && detail.Quantity > 0)
                    {
                        detail.RequestDetail_ID = Guid.NewGuid();
                        detail.Request_ID = model.Request.Request_ID;
                        _context.RequestDetails.Add(detail);
                    }
                }

                await _context.SaveChangesAsync();

                // ส่งค่า RequestNumber ไป View
                ViewBag.Success = true;
                ViewBag.RequestNumber = model.Request.RequestNumber;
            }

            // โหลดข้อมูลสำหรับ View อีกครั้ง (Employee, Jobs, Materials)
            model.Employees = await _context.Employees
                                            .Where(e => e.Role_ID == 202)
                                            .AsNoTracking()
                                            .OrderBy(e => e.Emp_Fname)
                                            .ToListAsync();

            var jobs = await _context.Jobs.AsNoTracking().OrderBy(j => j.JobsName).ToListAsync();
            ViewBag.Jobs = jobs;

            var materials = await _context.materials.Include(m => m.Unit).ToListAsync();
            ViewBag.Materials = materials ?? new List<Materials>();

            // กัน null
            if (model.RequestDetails.Count == 0)
                model.RequestDetails = new List<RequestDetail> { new RequestDetail() };

            return View(model);
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
                var requests = await _context.Requests
                    .Include(x => x.Employees)
                    .Include(x => x.Workflows)
                        .ThenInclude(w => w.Status)
                    .ToListAsync();
                return View(requests);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return View(new List<Request>());

            }
        }
    }
}
