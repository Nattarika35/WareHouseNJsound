using Microsoft.AspNetCore.Mvc;
using WareHouseNJsound.Data;
using System.Linq;
using Microsoft.AspNetCore.Http;
using System;

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
        public IActionResult Index(string username, string password)
        {
            try
            {
                var employee = _context.Employees
                    .FirstOrDefault(e => e.Username == username && e.Password == password);

                if (employee != null)
                {
                    HttpContext.Session.SetString("Username", employee.Username);
                    HttpContext.Session.SetString("FullName", employee.FullName ?? employee.Username);
                    if (employee.Picture != null && employee.Picture.Length > 0)
                    {
                        string base64Image = Convert.ToBase64String(employee.Picture);
                        string imageSrc = $"data:image/png;base64,{base64Image}"; // หรือ image/jpeg แล้วแต่ไฟล์จริง
                        HttpContext.Session.SetString("ProfileImage", imageSrc);
                    }
                    else
                    {
                        HttpContext.Session.SetString("ProfileImage", "/img/user.png");
                    }


                    return RedirectToAction("Index", "Home");
                }

                ViewBag.Error = "ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง";
            }
            catch (Exception ex)
            {
                ViewBag.Error = "เกิดข้อผิดพลาด: " + ex.Message;
            }

            return View();
        }

    }
}
