using Microsoft.AspNetCore.Mvc;
using WareHouseNJsound.Data;
using System.Linq;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Threading.Tasks;

namespace WareHouseNJsound.Controllers
{
    public class LoginController : Controller
    {
        private readonly CoreContext _context;

        public LoginController(CoreContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Index(string username, string password)
        {
            try
            {
                var employee = _context.Employees
                    .FirstOrDefault(e => e.Username == username && e.Password == password);
                // TODO: ควรเปลี่ยนมาใช้ password hash ในโปรดักชัน

                if (employee != null)
                {
                    // --- ใส่ SESSION (ไว้ใช้โชว์ชื่อ/รูป) ---
                    HttpContext.Session.SetString("Username", employee.Username);
                    HttpContext.Session.SetString("FullName", employee.FullName ?? employee.Username);
                    HttpContext.Session.SetString("Employee_ID", employee.Employee_ID ?? employee.Username);

                    if (employee.Picture != null && employee.Picture.Length > 0)
                    {
                        string base64Image = Convert.ToBase64String(employee.Picture);
                        string imageSrc = $"data:image/png;base64,{base64Image}";
                        HttpContext.Session.SetString("ProfileImage", imageSrc);
                    }
                    else
                    {
                        HttpContext.Session.SetString("ProfileImage", "/img/user.png");
                    }

                    // --- ออกคุกกี้ Authentication พร้อม Claims ---
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, employee.Employee_ID),
                        new Claim(ClaimTypes.Name, employee.FullName ?? employee.Username),
                        new Claim("RoleId", (employee.Role_ID ?? 0).ToString())
                    };
                    if (employee.Role_ID == 201)
                    {
                        claims.Add(new Claim(ClaimTypes.Role, "Admin"));
                    }


                    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var principal = new ClaimsPrincipal(identity);

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        principal,
                        new AuthenticationProperties
                        {
                            IsPersistent = true,              // ให้คุกกี้อยู่ยาวถ้าต้องการ
                            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                        });

                    return RedirectToAction("Dashboard", "Request");
                }

                ViewBag.Error = "ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง";
            }
            catch (Exception ex)
            {
                ViewBag.Error = "เกิดข้อผิดพลาด: " + ex.Message;
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            return RedirectToAction(nameof(Index));
        }
    }
}
