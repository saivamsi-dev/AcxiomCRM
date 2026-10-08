using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers;

[Authorize(Policy = "SalesAccess")]
public class CrmController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }
}