using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Miscord.Client.Models;

namespace Miscord.Client.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
    

    public IActionResult Privacy()
    {
        return View();
    }

    [Route("Home/Error/{statusCode}")]
    public IActionResult Error(int statusCode)
    {
        var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        
        return statusCode switch
        {
            404 => View("Error404", requestId),
            401 => View("Error401", requestId),
            400 => View("Error400", requestId),
            _   => View("Error500", requestId)
        };
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View("Error500", Activity.Current?.Id ?? HttpContext.TraceIdentifier);
    }
}
