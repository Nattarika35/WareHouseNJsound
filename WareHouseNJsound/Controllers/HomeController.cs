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
                .Include(p => p.Unit)
                .Include(p => p.Category)
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


        public IActionResult AddMaterial()
        {
            ViewBag.Categories = _context.Categories.ToList();
            ViewBag.Units = _context.Units.ToList();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMaterial(Materials materials, IFormFile PictureFile)
        {
            if (ModelState.IsValid)
            {
                if (PictureFile != null && PictureFile.Length > 0)
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        await PictureFile.CopyToAsync(memoryStream);
                        materials.Picture = memoryStream.ToArray(); // ใส่เป็น byte[]
                    }
                }

                _context.Add(materials);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "เพิ่มสินค้าเรียบร้อยแล้ว";
                return RedirectToAction(nameof(Index));
            }

            return View(materials);
        }

        // GET: Home/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null) return NotFound();

            var material = await _context.materials
                .Include(m => m.Category)
                .Include(m => m.Unit)
                .FirstOrDefaultAsync(m => m.Materials_ID == id);

            if (material == null) return NotFound();

            ViewBag.Categories = _context.Categories.ToList();
            ViewBag.Units = _context.Units.ToList();

            return View(material);
        }


        // POST: Home/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, Materials materials, IFormFile PictureFile)
        {
            if (id != materials.Materials_ID)
            {
                // กรณีที่แก้ไขรหัสสินค้า ต้องจัดการลบข้อมูลเก่าหรืออัปเดตคีย์หลัก
                // เพราะ EF Core ไม่รองรับเปลี่ยนค่า PK ตรงๆ
                var oldMaterial = await _context.materials.FindAsync(id);
                if (oldMaterial == null) return NotFound();

                // ลบข้อมูลเก่าที่มี PK เดิม
                _context.materials.Remove(oldMaterial);

                // ใส่ข้อมูลใหม่ (พร้อมรหัสที่แก้ไข)
                if (PictureFile != null && PictureFile.Length > 0)
                {
                    using var ms = new MemoryStream();
                    await PictureFile.CopyToAsync(ms);
                    materials.Picture = ms.ToArray();
                }
                else
                {
                    // ถ้าไม่ได้อัปโหลดรูปใหม่ ให้เก็บรูปเดิมจากข้อมูลเก่าไว้
                    materials.Picture = oldMaterial.Picture;
                }

                _context.materials.Add(materials);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "แก้ไขข้อมูลสำเร็จ";
                return RedirectToAction(nameof(Index));
            }

            // กรณีรหัสไม่เปลี่ยน (PK เดิม)
            if (ModelState.IsValid)
            {
                try
                {
                    if (PictureFile != null && PictureFile.Length > 0)
                    {
                        using var ms = new MemoryStream();
                        await PictureFile.CopyToAsync(ms);
                        materials.Picture = ms.ToArray();
                    }
                    else
                    {
                        // เก็บรูปเดิมจากฐานข้อมูล
                        var oldMaterial = await _context.materials.AsNoTracking().FirstOrDefaultAsync(m => m.Materials_ID == id);
                        materials.Picture = oldMaterial?.Picture;
                    }

                    _context.Update(materials);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "แก้ไขข้อมูลสำเร็จ";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.materials.Any(e => e.Materials_ID == id))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(materials);
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

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        
    }
}
