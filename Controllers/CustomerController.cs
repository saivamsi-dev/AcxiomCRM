using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Policy = "SalesAccess")]
public class CustomerController : Controller
{
    private readonly ApplicationDbContext _context;

    public CustomerController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Create(Customer customer)
    {
        customer.CustomerCode = $"CUS-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        customer.CreatedDate = DateTime.UtcNow;
        customer.CreatedBy = User.Identity?.Name ?? "System";
        customer.Status = "Active";

        ModelState.Remove(nameof(Customer.CustomerCode));
        ModelState.Remove(nameof(Customer.CreatedDate));
        ModelState.Remove(nameof(Customer.CreatedBy));
        ModelState.Remove(nameof(Customer.Status));

        if (!ModelState.IsValid)
        {
            return View(customer);
        }

        var emailExists = await _context.Customers
            .AnyAsync(c => c.Email == customer.Email);

        if (emailExists)
        {
            ModelState.AddModelError(nameof(Customer.Email),
                "A customer with this email already exists.");
            return View(customer);
        }

        var phoneExists = await _context.Customers
            .AnyAsync(c => c.Phone == customer.Phone);

        if (phoneExists)
        {
            ModelState.AddModelError(nameof(Customer.Phone),
                "A customer with this phone number already exists.");
            return View(customer);
        }

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var customers = await _context.Customers
            .OrderByDescending(c => c.CreatedDate)
            .ToListAsync();

        return View(customers);
    }
}