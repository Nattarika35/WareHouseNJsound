using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WareHouseNJsound.Data;
using WareHouseNJsound.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using ClosedXML.Excel;
using System.Text;
using System.Data;

namespace WareHouseNJsound.Controllers
{
    public class HomeController : Controller
    {
        
        private readonly ILogger<HomeController> _logger;
        private readonly CoreContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration Configuration;

        public HomeController(ILogger<HomeController> logger, CoreContext context, IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
        {
            _logger = logger;
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            Configuration = configuration;

        }

        public IActionResult Index(string category)
        {
            var query = _context.materials
                .AsNoTracking()
                .Include(p => p.Stock)
                .Include(p => p.Unit)
                .Include(p => p.Category)
                .Include(p => p.Type)
                .AsQueryable();

            if (!string.IsNullOrEmpty(category) && category.ToLower() != "all")
            {
                // หลีกเลี่ยง ToLower() เพื่อให้ใช้ Index ได้
                query = query.Where(p => p.Category.CategoryName == category);
            }

            // จำกัดข้อมูลเพื่อป้องกัน timeout
            var materials = query.ToList();

            var categories = _context.Categories
                .AsNoTracking()
                .Select(c => c.CategoryName)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            ViewBag.Categories = categories;
            ViewBag.SelectedCategory = category ?? "all";

            return View(materials);
        }

        public IActionResult Export(string category, string format = "csv")
        {
            var query = _context.materials
                .AsNoTracking()
                .Include(p => p.Stock)
                .Include(p => p.Unit)
                .Include(p => p.Type)
                .Include(p => p.Category)
                .AsQueryable();

            if (!string.IsNullOrEmpty(category) && category.ToLower() != "all")
            {
                query = query.Where(p => p.Category.CategoryName == category);
            }

            // 🔹 เลือกเฉพาะฟิลด์ที่ export
            var rows = query
                .Select(p => new
                {
                    p.Materials_ID,
                    p.MaterialsName,
                    Category = p.Category.CategoryName,
                    Type = p.Type.TypeName,
                    Unit = p.Unit.UnitName,
                    Stock = (int?)(p.Stock.OnHandStock) ?? 0,
                    p.MinimumStock,
                    Price = p.Price ?? 0m,
                    p.ReceivedDate,
                    p.WarrantyExpiryDate,
                    Remark = p.Description
                })
                .OrderBy(r => r.Materials_ID)
                .ToList();

            var fileName = $"materials_{(string.IsNullOrEmpty(category) ? "all" : category)}_{DateTime.Now:yyyyMMdd_HHmm}.";
            format = (format ?? "csv").ToLowerInvariant();

            // =========================
            // 📗 Excel (ClosedXML)
            // =========================
            if (format == "xlsx" || format == "excel")
            {
                using var wb = new XLWorkbook();
                var dt = new DataTable("Materials");

                dt.Columns.Add("Materials ID", typeof(string));
                dt.Columns.Add("Name", typeof(string));
                dt.Columns.Add("Category", typeof(string));
                dt.Columns.Add("Type", typeof(string));
                dt.Columns.Add("Unit", typeof(string));
                dt.Columns.Add("Stock", typeof(int));
                dt.Columns.Add("Minimum Stock", typeof(int));
                dt.Columns.Add("Price", typeof(decimal));
                dt.Columns.Add("Received Date", typeof(string));
                dt.Columns.Add("Warranty Expiry Date", typeof(string));
                dt.Columns.Add("Remark", typeof(string));

                foreach (var r in rows)
                {
                    dt.Rows.Add(
                        r.Materials_ID,
                        r.MaterialsName,
                        r.Category,
                        r.Type,
                        r.Unit,
                        r.Stock,
                        r.MinimumStock,
                        r.Price,
                        r.ReceivedDate.ToString("dd/MM/yyyy"),
                        r.WarrantyExpiryDate.ToString("dd/MM/yyyy"),
                        r.Remark
                    );
                }

                var ws = wb.Worksheets.Add(dt, "Export");
                ws.Columns().AdjustToContents();

                using var ms = new MemoryStream();
                wb.SaveAs(ms);

                return File(
                    ms.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName + "xlsx"
                );
            }

            // =========================
            // 📄 CSV (UTF-8 BOM)
            // =========================
            var sb = new StringBuilder();

            void AppendCsv(params object[] fields)
            {
                static string Esc(object o)
                {
                    if (o == null) return "";
                    var s = o.ToString();
                    if (s.Contains('"') || s.Contains(',') || s.Contains('\n') || s.Contains('\r'))
                        s = $"\"{s.Replace("\"", "\"\"")}\"";
                    return s;
                }
                sb.AppendLine(string.Join(",", fields.Select(Esc)));
            }

            // Header
            AppendCsv(
                "Materials ID", "Name", "Category", "Type", "Unit",
                "Stock", "Minimum Stock", "Price",
                "Received Date", "Warranty Expiry Date", "Remark"
            );

            // Rows
            foreach (var r in rows)
            {
                AppendCsv(
                    r.Materials_ID,
                    r.MaterialsName,
                    r.Category,
                    r.Type,
                    r.Unit,
                    r.Stock,
                    r.MinimumStock,
                    r.Price,
                    r.ReceivedDate.ToString("dd/MM/yyyy"),
                    r.WarrantyExpiryDate.ToString("dd/MM/yyyy"),
                    r.Remark
                );
            }

            var utf8Bom = new UTF8Encoding(true);
            var bytes = utf8Bom.GetBytes(sb.ToString());

            return File(bytes, "text/csv", fileName + "csv");
        }

        public IActionResult AddMaterial()
        {
            ViewBag.Types = _context.Types.ToList();
            ViewBag.Categories = _context.Categories.ToList();
            ViewBag.Units = _context.Units.ToList();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddMaterial(Materials materials, IFormFile PictureFile)
        {
            if (!ModelState.IsValid)
                return View(materials);

            // อ่านรูปเป็น byte[]
            if (PictureFile != null && PictureFile.Length > 0)
            {
                using var ms = new MemoryStream();
                await PictureFile.CopyToAsync(ms);
                materials.Picture = ms.ToArray();
            }

            // ใช้ทรานแซกชันกันค้างครึ่งทาง
            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1) บันทึกตัวสินค้า
                _context.materials.Add(materials);
                await _context.SaveChangesAsync();

                // 2) สร้าง/อัปเดต stock สำหรับสินค้านี้
                var qty = materials.OnHandStock ?? 0;   // มาจากฟอร์ม
                var stock = await _context.Stocks
                    .SingleOrDefaultAsync(s => s.Materials_ID == materials.Materials_ID);

                if (stock == null)
                {
                    stock = new Stock
                    {
                        Materials_ID = materials.Materials_ID,
                        OnHandStock = qty,
                    };
                    _context.Stocks.Add(stock);
                }
                else
                {
                    // เลือก logic ได้: จะ "ตั้งค่าเท่ากับ" หรือ "บวกเพิ่ม"
                    //stock.OnHandStock = qty;
                    stock.OnHandStock += qty;
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                TempData["SuccessMessage"] = "เพิ่มสินค้าเรียบร้อยแล้ว";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                ModelState.AddModelError("", "บันทึกไม่สำเร็จ: " + ex.Message);
                return View(materials);
            }
        }

        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var material = await _context.materials
                .Include(m => m.Stock)
                .Include(m => m.Category)
                .Include(m => m.Unit)
                .Include(m => m.Type)

                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Materials_ID == id);

            if (material == null) return NotFound();

            // ดึงค่าจากตาราง Stock มาใส่ฟิลด์ที่ใช้โชว์ในฟอร์ม
            material.OnHandStock = material.Stock?.OnHandStock ?? 0;

            ViewBag.Categories = await _context.Categories.AsNoTracking().ToListAsync();
            ViewBag.Units = await _context.Units.AsNoTracking().ToListAsync();
            ViewBag.Types = await _context.Types.AsNoTracking().ToListAsync();

            return View(material);
        }


        [HttpPost]
        public async Task<IActionResult> Edit(string id, Materials input, IFormFile PictureFile)
        {
            if (id != input.Materials_ID)
            {
                ModelState.AddModelError("", "ไม่อนุญาตให้แก้ไขรหัสสินค้า");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Categories = await _context.Categories.ToListAsync();
                ViewBag.Units = await _context.Units.ToListAsync();
                ViewBag.Types = await _context.Types.ToListAsync();
                return View(input);
            }

            var mat = await _context.materials
                .Include(m => m.Stock)
                .FirstOrDefaultAsync(m => m.Materials_ID == id);

            if (mat == null) return NotFound();

            // อัปเดตข้อมูลพื้นฐาน
            mat.MaterialsName = input.MaterialsName;
            mat.Category_ID = input.Category_ID;
            mat.Type_ID = input.Type_ID;
            mat.Unit_ID = input.Unit_ID;
            mat.MinimumStock = input.MinimumStock;
            mat.Price = input.Price;
            mat.ReceivedDate = input.ReceivedDate;
            mat.WarrantyExpiryDate = input.WarrantyExpiryDate;
            mat.Description = input.Description;

            // รูปภาพ
            if (PictureFile != null && PictureFile.Length > 0)
            {
                using var ms = new MemoryStream();
                await PictureFile.CopyToAsync(ms);
                mat.Picture = ms.ToArray();
            }

            // อัปเดตสต๊อก (ตั้งค่าให้เท่ากับค่าที่กรอกมา)
            var newQty = input.OnHandStock ?? mat.Stock?.OnHandStock ?? 0;
            if (mat.Stock == null)
            {
                mat.Stock = new Stock
                {
                    Materials_ID = mat.Materials_ID,
                    OnHandStock = newQty,
                };
                _context.Stocks.Add(mat.Stock);
            }
            else
            {
                mat.Stock.OnHandStock = newQty; // หรือจะทำเป็น + เพิ่ม ก็เปลี่ยนเป็น += ได้
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "แก้ไขข้อมูลสำเร็จ";
            return RedirectToAction(nameof(Index));
        }



        // GET: Home/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null) return NotFound();

            var material = await _context.materials
                .FirstOrDefaultAsync(m => m.Materials_ID == id);

            if (material == null) return NotFound();

            _context.materials.Remove(material);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "ลบข้อมูลสำเร็จ";
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Admin()
        {
            var employees = _context.Employees
                .Where(e => e.Role_ID == 201)
                .ToList();

            return View(employees);
        }

        [HttpPost]
        public IActionResult DeleteAdmin(string id)
        {
            var employee = _context.Employees.FirstOrDefault(e => e.Employee_ID == id && e.Role_ID == 201);
            if (employee == null)
            {
                return NotFound();
            }

            _context.Employees.Remove(employee);
            _context.SaveChanges();

            return Json(new { success = true });
        }


        // GET: /Account/ChangePassword
        public IActionResult ChangePassword()
            {
            string employeeId = HttpContext.Session.GetString("Employee_ID");

            var employee = _context.Employees.FirstOrDefault(e => e.Employee_ID == employeeId);
            if (employee == null) return NotFound();

            return View(employee);
        }

        // POST: /Account/ChangePassword
        [HttpPost]
        public IActionResult ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            // ดึง Employee จาก session หรือ identity (สมมติใช้ Session)
            string employeeId = HttpContext.Session.GetString("Employee_ID");
 

            var employee = _context.Employees.FirstOrDefault(e => e.Employee_ID == employeeId);
            if (employee == null)
            {
                ViewBag.Message = "ไม่พบข้อมูลพนักงาน";
                return View();
            }

            if (newPassword != confirmPassword)
            {
                ViewBag.Message = "รหัสผ่านใหม่ไม่ตรงกัน";
                return View();
            }

            if (employee.Password != currentPassword) // ถ้าเก็บ hashed ต้องเปลี่ยนเป็นตรวจสอบ hash
            {
                ViewBag.Message = "รหัสผ่านปัจจุบันไม่ถูกต้อง";
                return View();
            }

            // เปลี่ยนรหัสผ่าน
            employee.Password = newPassword;
            _context.SaveChanges();

            ViewBag.Message = "เปลี่ยนรหัสผ่านสำเร็จ!";
            return View();
        }
        public IActionResult Employee()
        {
            var employees = _context.Employees
                .Where(e => e.Role_ID == 202)
                .ToList();

            return View(employees);
        }

        [HttpGet]
        public async Task<IActionResult> AdminCreate()
        {
            await PopulateDropdowns();
            return View(new Employee { Role_ID = 201 });
        }

        [HttpPost]
        public async Task<IActionResult> AdminCreate(Employee vm)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdowns(); // กลับไปหน้าเดิม ให้ dropdown ยังมีค่า
                return View(vm);
            }

            // เช็คซ้ำ username
            var exists = await _context.Employees
                .AnyAsync(e => e.Username == vm.Username);
            if (exists)
            {
                ModelState.AddModelError(nameof(vm.Username), "Username นี้ถูกใช้งานแล้ว");
                return View(vm);
            }

            // แปลงรูปเป็น byte[]
            byte[] pictureBytes = null;
            if (vm.PictureFile != null && vm.PictureFile.Length > 0)
            {
                using (var ms = new MemoryStream())
                {
                    await vm.PictureFile.CopyToAsync(ms);
                    pictureBytes = ms.ToArray();
                }
            }



            var emp = new Employee
            {
                Employee_ID = vm.Employee_ID,
                Picture = pictureBytes,
                Username = vm.Username,
                // *** ควร Hash Password จริงจังในโปรดักชั่น ***
                Password = vm.Password,
                Emp_Fname = vm.Emp_Fname,
                Emp_Lname = vm.Emp_Lname,
                Emp_Tel = vm.Emp_Tel,
                Email = vm.Email,
                Address = vm.Address,
                Brithdate = vm.Brithdate,
                Gender_ID = vm.Gender_ID,
                Role_ID = 201, // 201 = Admin
                Personal_ID = vm.Personal_ID
            };

            _context.Employees.Add(emp);
            await _context.SaveChangesAsync();

            // ส่ง TempData ไปแสดง SweetAlert ที่หน้า Admin()
            TempData["AdminCreateSuccess"] = $"เพิ่มแอดมิน {emp.Emp_Fname} {emp.Emp_Lname} เรียบร้อย";
            return RedirectToAction(nameof(Admin));
        }

        private async Task PopulateDropdowns()
        {
            var genders = await _context.genders
                .AsNoTracking()
                .OrderBy(g => g.Gender_ID)
                .ToListAsync();

            ViewBag.Genders = new SelectList(genders, "Gender_ID", "GenderName");
        }

        [HttpGet]
        public async Task<IActionResult> AdminEdit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest();

            var emp = await _context.Employees
                .FirstOrDefaultAsync(e => e.Employee_ID == id && e.Role_ID == 201); // 201 = Admin
            if (emp == null) return NotFound();

            await PopulateDropdowns();
            return View(emp);
        }

        [HttpPost]
        public async Task<IActionResult> AdminEdit(string id, Employee model, bool RemovePicture = false)
        {
            if (string.IsNullOrWhiteSpace(id) || id != model.Employee_ID) return BadRequest();

            // ตรวจ username ซ้ำ (ยกเว้นของตัวเอง)
            var usernameTaken = await _context.Employees
                .AnyAsync(e => e.Username == model.Username && e.Employee_ID != id);
            if (usernameTaken)
            {
                // ✅ แก้ตรงนี้
                ModelState.AddModelError("Username", "Username นี้ถูกใช้งานแล้ว");
                // หรือ: ModelState.AddModelError("Username", "Username นี้ถูกใช้งานแล้ว");
            }

            // เช็ครหัสผ่าน (ถ้ากรอกมา จะต้องตรงกับ Confirm)
            if (!string.IsNullOrWhiteSpace(model.Password) &&
                model.Password != model.ConfirmPassword)
            {
                ModelState.AddModelError("ConfirmPassword", "รหัสผ่านและยืนยันรหัสผ่านไม่ตรงกัน");
            }

            if (!ModelState.IsValid)
            {
                await PopulateDropdowns();
                return View(model);
            }

            var emp = await _context.Employees
                .FirstOrDefaultAsync(e => e.Employee_ID == id && e.Role_ID == 201);
            if (emp == null) return NotFound();

            // อัปเดตฟิลด์
            emp.Username = model.Username;
            if (!string.IsNullOrWhiteSpace(model.Password))
                emp.Password = model.Password; // โปรดเปลี่ยนเป็น Hash ในโปรดักชัน

            emp.Emp_Fname = model.Emp_Fname;
            emp.Emp_Lname = model.Emp_Lname;
            emp.Emp_Tel = model.Emp_Tel;
            emp.Email = model.Email;
            emp.Address = model.Address;
            emp.Brithdate = model.Brithdate;
            emp.Gender_ID = model.Gender_ID;
            emp.Role_ID = 201; // คงเป็นแอดมิน

            // รูปภาพ
            if (RemovePicture)
            {
                emp.Picture = null;
            }
            else if (model.PictureFile != null && model.PictureFile.Length > 0)
            {
                using var ms = new MemoryStream();
                await model.PictureFile.CopyToAsync(ms);
                emp.Picture = ms.ToArray();
            }

            await _context.SaveChangesAsync();

            TempData["AdminUpdateSuccess"] = $"อัปเดตข้อมูลแอดมิน {emp.FullName} เรียบร้อย";
            return RedirectToAction(nameof(Admin));
        }

        [HttpGet]
        public async Task<IActionResult> EmployeeCreate()
        {
            await PopulateEmployeeDropdowns(); // โหลดเพศ
            return View(new Employee { Role_ID = 202 }); // 202 = พนักงาน
        }

        // POST: /Home/EmployeeCreate
        [HttpPost]
        public async Task<IActionResult> EmployeeCreate(Employee model)
        {
            

            byte[] pictureBytes = null;
            if (model.PictureFile != null && model.PictureFile.Length > 0)
            {
                using (var ms = new MemoryStream())
                {
                    await model.PictureFile.CopyToAsync(ms);
                    pictureBytes = ms.ToArray();
                }
            }

         
            // กำหนดสิทธิ์เป็นพนักงาน (ถ้าอยากให้เลือกได้ คอมเมนต์บรรทัดนี้)
            model.Role_ID = 202;

            // TODO: โปรดเปลี่ยนเป็นการ Hash Password ในโปรดักชัน
            var emp = new Employee
            {
                Employee_ID = model.Employee_ID,
                Picture = pictureBytes,
                Username = model.Username,
                // *** ควร Hash Password จริงจังในโปรดักชั่น ***
                Password = model.Password,
                Emp_Fname = model.Emp_Fname,
                Emp_Lname = model.Emp_Lname,
                Emp_Tel = model.Emp_Tel,
                Email = model.Email,
                Address = model.Address,
                Brithdate = model.Brithdate,
                Gender_ID = model.Gender_ID,
                Role_ID = 202, // 201 = Admin
                Personal_ID = model.Personal_ID
            };
            _context.Employees.Add(emp);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"เพิ่มพนักงาน {model.FullName} เรียบร้อย";
            return RedirectToAction(nameof(Employee)); // ไปหน้ารายการพนักงาน
        }

        private async Task PopulateEmployeeDropdowns()
        {
            var genders = await _context.genders
                .AsNoTracking()
                .OrderBy(g => g.Gender_ID)
                .ToListAsync();

            ViewBag.Genders = new SelectList(genders, "Gender_ID", "GenderName");
        }

        // ถ้ายังไม่มี helper นี้ ให้ใส่ไว้
        private async Task<string> GenerateEmployeeIdAsync()
        {
            var ids = await _context.Employees
                .Select(e => e.Employee_ID)
                .ToListAsync();

            // รูปแบบ E-### (ปรับตามแพทเทิร์นของคุณได้)
            var max = ids.Select(id =>
            {
                if (string.IsNullOrWhiteSpace(id)) return 0;
                var s = id.StartsWith("E-") ? id.Substring(2) : id;
                return int.TryParse(s, out var n) ? n : 0;
            }).DefaultIfEmpty(0).Max();

            return $"E-{max + 1}";
        }

        [HttpGet]
        public async Task<IActionResult> EmployeeEdit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest();

            var emp = await _context.Employees.FirstOrDefaultAsync(e => e.Employee_ID == id);
            if (emp == null) return NotFound();

            await PopulateEmployeeDropdowns(); // โหลดเพศ
            return View(emp);
        }

        // POST: /Home/EmployeeEdit/{id}
        [HttpPost]
        public async Task<IActionResult> EmployeeEdit(string id, Employee model, bool RemovePicture = false)
        {
            if (string.IsNullOrWhiteSpace(id) || id != model.Employee_ID) return BadRequest();

       
            

            var emp = await _context.Employees.FirstOrDefaultAsync(e => e.Employee_ID == id);
            if (emp == null) return NotFound();

            // อัปเดตฟิลด์
            emp.Username = model.Username;
            if (!string.IsNullOrWhiteSpace(model.Password))
            {
                emp.Password = model.Password; // TODO: โปรดเปลี่ยนเป็น Hash ในโปรดักชัน
            }
            emp.Emp_Fname = model.Emp_Fname;
            emp.Emp_Lname = model.Emp_Lname;
            emp.Emp_Tel = model.Emp_Tel;
            emp.Email = model.Email;
            emp.Address = model.Address;
            emp.Brithdate = model.Brithdate;
            emp.Gender_ID = model.Gender_ID;
            emp.Personal_ID = model.Personal_ID;
            // ถ้ามี Role_ID ให้แก้ด้วยตามต้องการ (เช่น คง 202 เป็นพนักงาน)

            // รูปภาพ
            if (RemovePicture)
            {
                emp.Picture = null;
            }
            else if (model.PictureFile != null && model.PictureFile.Length > 0)
            {
                using var ms = new MemoryStream();
                await model.PictureFile.CopyToAsync(ms);
                emp.Picture = ms.ToArray();
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"อัปเดตข้อมูลพนักงาน {emp.FullName} สำเร็จ";
            return RedirectToAction(nameof(Employee));   // ไปหน้า list นี้

        }

        [HttpPost]
        public async Task<IActionResult> EmployeeDelete(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest(new { ok = false, message = "ไม่พบรหัสพนักงาน" });

            var emp = await _context.Employees.FirstOrDefaultAsync(e => e.Employee_ID == id);
            if (emp == null)
                return NotFound(new { ok = false, message = "ไม่พบข้อมูลพนักงาน" });

            // ถ้าอยากกันไม่ให้ลบแอดมิน:
            // if (emp.Role_ID == 201) return StatusCode(403, new { ok=false, message="ไม่อนุญาตให้ลบแอดมิน" });

            _context.Employees.Remove(emp);
            try
            {
                await _context.SaveChangesAsync();
                return Json(new { ok = true, message = $"ลบ {emp.FullName} สำเร็จ" });
            }
            catch (DbUpdateException)
            {
                return StatusCode(409, new { ok = false, message = "ลบไม่ได้: มีข้อมูลอื่นอ้างอิงอยู่" });
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpGet]
        public IActionResult Receipt(string? category)
        {
            var query = _context.materials
                .Include(p => p.Stock)
                .Include(p => p.Unit)
                .Include(p => p.Category)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(category) && !string.Equals(category, "all", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.Category.CategoryName.ToLower() == category!.ToLower());
            }

            var products = query.OrderBy(p => p.MaterialsName).ToList();

            var categories = _context.Categories
                .Select(c => c.CategoryName)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            ViewBag.Categories = categories;
            ViewBag.SelectedCategory = string.IsNullOrWhiteSpace(category) ? "all" : category;

            // model = รายการสินค้าให้รับเข้า
            return View(products);
        }

        public class ReceiptPostVM
        {
            public DateTime ReceiveDate { get; set; } = DateTime.Now;
            public string DocumentNo { get; set; } = "";
            public string? Note { get; set; }
            public List<ReceiptItemVM> Items { get; set; } = new();
        }
        public class ReceiptItemVM
        {
            public string Materials_ID { get; set; } = default!;
            public int Quantity { get; set; }
            public string? Remark { get; set; }
        }


        // POST: บันทึกรับเข้า
        [HttpPost]
 
        public async Task<IActionResult> Receipt(ReceiptPostVM vm)
        {
            // กันโพสต์ว่าง ๆ
            var validItems = vm.Items.Where(i => !string.IsNullOrWhiteSpace(i.Materials_ID) && i.Quantity > 0).ToList();
            if (validItems.Count == 0)
            {
                TempData["ErrorMessage"] = "กรุณาใส่จำนวนรับเข้าอย่างน้อย 1 รายการ";
                return RedirectToAction(nameof(Receipt), new { category = "all" });
            }

            await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var now = vm.ReceiveDate == default ? DateTime.Now : vm.ReceiveDate;
                var docNo = string.IsNullOrWhiteSpace(vm.DocumentNo)
                    ? $"GR{DateTime.Now:yyMMddHHmmss}"
                    : vm.DocumentNo;

                foreach (var it in validItems)
                {
                    var mat = await _context.materials
                        .Include(m => m.Stock)
                        .FirstOrDefaultAsync(m => m.Materials_ID == it.Materials_ID)
                        ?? throw new InvalidOperationException($"ไม่พบรหัสสินค้า {it.Materials_ID}");

                    // ถ้ายังไม่มี stock row ให้สร้าง
                    mat.Stock ??= new Stock { Materials_ID = mat.Materials_ID, OnHandStock = 0 };

                    // บวกสต๊อก
                    mat.Stock.OnHandStock += it.Quantity;

                    // Insert Transaction: TranType_ID = 1 (รับเข้า)
                    _context.Transactions.Add(new Transaction
                    {
                        Transaction_Date = now,
                        TranType_ID = 1,
                        Quantity = it.Quantity,
                        Materials_ID = mat.Materials_ID,
                        Request_ID = null,               // รับเข้าทั่วไป (สคีมาคุณ Allow Nulls)
                        RequestNumber = docNo,
                        Employee_ID = /* รหัสผู้ทำรายการ */ "NUll",
                        Description = string.IsNullOrWhiteSpace(it.Remark) ? vm.Note : it.Remark
                    });
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                TempData["SuccessMessage"] = $"บันทึกรับเข้า {validItems.Count} รายการ (เลขที่ {docNo}) สำเร็จ";
                return RedirectToAction(nameof(Receipt), new { category = ViewBag?.SelectedCategory ?? "all" });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                TempData["ErrorMessage"] = ex.GetBaseException()?.Message ?? ex.Message;
                return RedirectToAction(nameof(Receipt), new { category = "all" });
            }
        }


  

        private string RunDocNo()
        {
            // TODO: ใส่เลขรันเอกสารรับเข้า
            return "GR" + DateTime.Now.ToString("yyMMddHHmm");
        }
    }
}
