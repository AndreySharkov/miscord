using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Miscord.Client.Models;
using Miscord.Data.Models;
using System.Threading.Tasks;

namespace Miscord.Client.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;

        // Dependency Injection brings in the SignInManager
        public AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager; 

        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken] // Protects against Cross-Site Request Forgery (CSRF)
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Attempt to sign in the user
                var result = await _signInManager.PasswordSignInAsync(
                    model.Email, 
                    model.Password, 
                    model.RememberMe, 
                    lockoutOnFailure: false);
                
                if (result.Succeeded)
                {
                    return RedirectToAction("Index", "Home"); // Send them to the homepage on success
                }
                
                // If it fails, add an error to show on the page
                ModelState.AddModelError(string.Empty, "Invalid login attempt. Please check your credentials.");
            }

            // If we got this far, something failed, redisplay the form
            return View(model);
        }

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser { 
                    UserName = model.Username, 
                    Email = model.Email,
                    CreatedAt = DateTime.UtcNow
                };
                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "User");
                    await _signInManager.SignInAsync(user, isPersistent: false);

                    return RedirectToAction("Index", "Home");
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            return View(model);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUsername(string username, string currentPassword)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }
            var passwordCheck = await _userManager.CheckPasswordAsync(user, currentPassword);
            if (!passwordCheck)
            {
                return Json(new { success = false, message = "Incorrect current password." });
            }
            if (string.IsNullOrWhiteSpace(username))
            {
                return Json(new { success = false, message = "Username cannot be empty." });
            }

            var existingUser = await _userManager.FindByNameAsync(username);
            if (existingUser != null && existingUser.Id != user.Id)
            {
                return Json(new { success = false, message = "Username is already taken." });
            }

            user.UserName = username;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user); // Refresh the sign-in to update claims
                return Json(new { success = true, message = "Username updated successfully." });
            }
            else
            {
                var errors = string.Join(" ", result.Errors.Select(e => e.Description));
                return Json(new { success = false, message = errors });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateNickname(string? nickname)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            user.Nickname = nickname;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user); // Refresh the sign-in to update claims
                return Json(new { success = true, message = "Nickname updated successfully." });
            }
            else
            {
                var errors = string.Join(" ", result.Errors.Select(e => e.Description));
                return Json(new { success = false, message = errors });
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                return Json(new { success = false, message = "New password and confirmation do not match." });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);
                return Json(new { success = true, message = "Password updated successfully!" });
            }

            var errors = string.Join(" ", result.Errors.Select(e => e.Description));
            return Json(new { success = false, message = errors });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(string? pronouns, string? bio, IFormFile? profilePicture)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            user.Pronouns = pronouns;
            user.Bio = bio;

            if (profilePicture != null && profilePicture.Length > 0)
            {
                using (var ms = new MemoryStream())
                {
                    await profilePicture.CopyToAsync(ms);
                    user.ProfilePictureData = ms.ToArray();
                }
            }

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                return Json(new { success = true, message = "Profile updated successfully!" });
            }

            var errors = string.Join(" ", result.Errors.Select(e => e.Description));
            return Json(new { success = false, message = errors });
        }

    }
}